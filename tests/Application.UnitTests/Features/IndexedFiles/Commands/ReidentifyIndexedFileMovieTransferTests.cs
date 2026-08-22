using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class ReidentifyIndexedFileMovieTransferTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ReidentifyIndexedFileCommandHandler _handler = null!;
    private Guid _libraryId;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media/movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        _handler = new ReidentifyIndexedFileCommandHandler(
            _context,
            sender,
            availability,
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance),
            Substitute.For<ILogger<ReidentifyIndexedFileCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldTransferWatchStateAndDeleteOrphanMovie_WhenReidentifiedToExistingMovie()
    {
        var wrongMovie = new Movie { Id = Guid.NewGuid(), Title = "Wrong Movie" };
        var correctMovie = new Movie { Id = Guid.NewGuid(), Title = "Correct Movie" };
        correctMovie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "tmdb-correct" });

        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = "movie.mkv",
            Extension = ".mkv",
            Path = "/media/movies/movie.mkv",
            ParentDirectory = "movies",
            Hash = 1,
            Size = 1,
            MediaId = wrongMovie.Id
        };
        wrongMovie.IndexedFiles = [file];

        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.Medias.AddRange(wrongMovie, correctMovie);
        _context.IndexedFiles.Add(file);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = wrongMovie.Id,
            PlayCount = 1,
            LastInteractedAt = DateTime.UtcNow.AddHours(-2)
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new ReidentifyIndexedFileCommand
        {
            IndexedFileId = file.Id,
            SelectedProvider = "tmdb",
            SelectedExternalId = "tmdb-correct"
        }, CancellationToken.None);

        var attached = await _context.IndexedFiles.SingleAsync(f => f.Id == file.Id);
        attached.MediaId.Should().Be(correctMovie.Id);

        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == wrongMovie.Id)).Should().BeFalse();

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(correctMovie.Id);
        state.PlayCount.Should().Be(1);
    }
}
