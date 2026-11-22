using System.Data.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace K7.Server.Infrastructure.IntegrationTests;

[TestFixture]
public class MediaLibraryAvailabilityPostgresTests
{
    private ITestDatabase _database = null!;
    private Guid _libraryId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabaseFactory.CreateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _database.ResetAsync();

        _libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using var context = CreateContext();
        context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Integration Movies",
            MediaType = LibraryMediaType.Movie
        });
        context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Integration Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task RebuildForLibraryAsync_ShouldInsertMissingAndRemoveStalePairs_WhenTableIsOutOfDate()
    {
        Guid linkedMovieId;
        Guid missingMovieId;
        var unlinkedMovieId = Guid.NewGuid();

        await using (var seedContext = CreateContext())
        {
            linkedMovieId = AddMovieWithFile(seedContext, "Inception");
            missingMovieId = AddMovieWithFile(seedContext, "Tenet");
            seedContext.Medias.Add(new Movie { Id = unlinkedMovieId, Title = "Dunkirk" });
            seedContext.MediaLibraryAvailabilities.AddRange(
                new MediaLibraryAvailability { LibraryId = _libraryId, MediaId = linkedMovieId },
                new MediaLibraryAvailability { LibraryId = _libraryId, MediaId = unlinkedMovieId });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext();
        await CreateService(context).RebuildForLibraryAsync(_libraryId);

        var mediaIds = await context.MediaLibraryAvailabilities
            .Where(a => a.LibraryId == _libraryId)
            .Select(a => a.MediaId)
            .ToListAsync();
        mediaIds.Should().BeEquivalentTo([linkedMovieId, missingMovieId]);
    }

    [Test]
    public async Task RebuildForLibraryAsync_ShouldNotFailAnyCommand_WhenAnotherWriterInsertsTheSamePairFirst()
    {
        Guid movieId;
        await using (var seedContext = CreateContext())
        {
            movieId = AddMovieWithFile(seedContext, "Inception");
            await seedContext.SaveChangesAsync();
        }

        // Plays the concurrent CreateMedia task: it commits the pair after the rebuild has read
        // the table, right before the rebuild sends its own insert for that pair.
        var racingWriter = new RacingWriterInterceptor(async () =>
        {
            await using var otherContext = CreateContext();
            otherContext.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
            {
                LibraryId = _libraryId,
                MediaId = movieId
            });
            await otherContext.SaveChangesAsync();
        });

        await using var context = CreateContext(racingWriter);
        var act = () => CreateService(context).RebuildForLibraryAsync(_libraryId);

        await act.Should().NotThrowAsync();
        racingWriter.HasRun.Should().BeTrue();
        racingWriter.FailedCommands.Should().BeEmpty();
        (await context.MediaLibraryAvailabilities.CountAsync(a =>
            a.LibraryId == _libraryId && a.MediaId == movieId)).Should().Be(1);
    }

    private static MediaLibraryAvailabilityService CreateService(ApplicationDbContext context) =>
        new(
            context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

    private Guid AddMovieWithFile(ApplicationDbContext context, string title)
    {
        var movieId = Guid.NewGuid();
        context.Medias.Add(new Movie { Id = movieId, Title = title });
        context.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            MediaId = movieId,
            Name = title,
            Extension = ".mkv",
            Path = $"/media/{title}.mkv",
            Hash = 1,
            Size = 1
        });
        return movieId;
    }

    private ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class RacingWriterInterceptor(Func<Task> writeAsync) : DbCommandInterceptor
    {
        public bool HasRun { get; private set; }

        public List<string> FailedCommands { get; } = [];

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!HasRun && command.CommandText.StartsWith("INSERT INTO \"MediaLibraryAvailabilities\"", StringComparison.Ordinal))
            {
                HasRun = true;
                await writeAsync();
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            FailedCommands.Add(eventData.Exception.Message);
            return base.CommandFailedAsync(command, eventData, cancellationToken);
        }
    }
}
