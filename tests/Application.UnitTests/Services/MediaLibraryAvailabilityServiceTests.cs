using System.Data.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class MediaLibraryAvailabilityServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private MediaLibraryAvailabilityService _sut = null!;
    private Guid _libraryId;
    private Guid _groupId;

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

        _groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _sut = new MediaLibraryAvailabilityService(
            _context,
            _cacheInvalidator,
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task EnsureFromIndexedFilesAsync_ShouldInsertMissingPairs_AndInvalidateCache()
    {
        var movieId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Inception" });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            MediaId = movieId,
            Name = "Inception",
            Extension = ".mkv",
            Path = "/media/Inception.mkv",
            Hash = 1,
            Size = 1
        });
        await _context.SaveChangesAsync();

        await _sut.EnsureFromIndexedFilesAsync(_libraryId, [fileId]);

        (await _context.MediaLibraryAvailabilities.CountAsync(a =>
            a.LibraryId == _libraryId && a.MediaId == movieId)).Should().Be(1);
        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task EnsureFromIndexedFilesAsync_ShouldBeIdempotent_WhenPairAlreadyExists()
    {
        var movieId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Inception" });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            MediaId = movieId,
            Name = "Inception",
            Extension = ".mkv",
            Path = "/media/Inception.mkv",
            Hash = 1,
            Size = 1
        });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = _libraryId,
            MediaId = movieId
        });
        await _context.SaveChangesAsync();

        await _sut.EnsureFromIndexedFilesAsync(_libraryId, [fileId]);

        (await _context.MediaLibraryAvailabilities.CountAsync()).Should().Be(1);
        _cacheInvalidator.DidNotReceive().InvalidateAll();
    }

    [Test]
    public async Task EnsureFromIndexedFilesAsync_ShouldIncludeSerieParents_WhenFileIsEpisode()
    {
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        _context.Medias.Add(new Serie { Id = serieId, Title = "Show" });
        _context.Medias.Add(new SerieSeason { Id = seasonId, Title = "Season 1", SerieId = serieId, SeasonNumber = 1 });
        _context.Medias.Add(new SerieEpisode
        {
            Id = episodeId,
            Title = "Pilot",
            SerieId = serieId,
            SeasonId = seasonId,
            EpisodeNumber = 1
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            MediaId = episodeId,
            Name = "Pilot",
            Extension = ".mkv",
            Path = "/media/Pilot.mkv",
            Hash = 1,
            Size = 1
        });
        await _context.SaveChangesAsync();

        await _sut.EnsureFromIndexedFilesAsync(_libraryId, [fileId]);

        var mediaIds = await _context.MediaLibraryAvailabilities
            .Where(a => a.LibraryId == _libraryId)
            .Select(a => a.MediaId)
            .ToListAsync();

        mediaIds.Should().BeEquivalentTo([episodeId, seasonId, serieId]);
    }

    [Test]
    public async Task EnsureFromIndexedFilesAsync_ShouldNotThrow_WhenTwoWritersRaceOnSamePair()
    {
        var movieId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Inception" });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            MediaId = movieId,
            Name = "Inception",
            Extension = ".mkv",
            Path = "/media/Inception.mkv",
            Hash = 1,
            Size = 1
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var context2 = new ApplicationDbContext(options);
        var sut2 = new MediaLibraryAvailabilityService(
            context2,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        var task1 = _sut.EnsureFromIndexedFilesAsync(_libraryId, [fileId]);
        var task2 = sut2.EnsureFromIndexedFilesAsync(_libraryId, [fileId]);
        await Task.WhenAll(task1, task2);

        (await _context.MediaLibraryAvailabilities.CountAsync(a =>
            a.LibraryId == _libraryId && a.MediaId == movieId)).Should().Be(1);
    }

    [Test]
    public async Task InsertPairsAsync_ShouldSkipExistingPairs_WhenAnotherWriterInsertedThem()
    {
        var existingMovieId = await AddMovieWithFileAsync(_libraryId, "Inception");
        var newMovieId = await AddMovieWithFileAsync(_libraryId, "Tenet");
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = _libraryId,
            MediaId = existingMovieId
        });
        await _context.SaveChangesAsync();

        var inserted = await _sut.InsertPairsAsync(
        [
            new MediaLibraryPair(_libraryId, existingMovieId),
            new MediaLibraryPair(_libraryId, newMovieId)
        ]);

        inserted.Should().Be(1);
        var mediaIds = await _context.MediaLibraryAvailabilities
            .Where(a => a.LibraryId == _libraryId)
            .Select(a => a.MediaId)
            .ToListAsync();
        mediaIds.Should().BeEquivalentTo([existingMovieId, newMovieId]);
    }

    [Test]
    public async Task RebuildForLibraryAsync_ShouldInsertMissingAndRemoveStalePairs_WhenTableIsOutOfDate()
    {
        var otherLibraryId = AddLibrary("Other movies");
        var linkedMovieId = await AddMovieWithFileAsync(_libraryId, "Inception");
        var missingMovieId = await AddMovieWithFileAsync(_libraryId, "Tenet");
        var unlinkedMovieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = unlinkedMovieId, Title = "Dunkirk" });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { LibraryId = _libraryId, MediaId = linkedMovieId },
            new MediaLibraryAvailability { LibraryId = _libraryId, MediaId = unlinkedMovieId },
            new MediaLibraryAvailability { LibraryId = otherLibraryId, MediaId = unlinkedMovieId });
        await _context.SaveChangesAsync();

        await _sut.RebuildForLibraryAsync(_libraryId);

        var pairs = await _context.MediaLibraryAvailabilities
            .Select(a => new { a.LibraryId, a.MediaId })
            .ToListAsync();
        pairs.Should().BeEquivalentTo(
        [
            new { LibraryId = _libraryId, MediaId = linkedMovieId },
            new { LibraryId = _libraryId, MediaId = missingMovieId },
            new { LibraryId = otherLibraryId, MediaId = unlinkedMovieId }
        ]);
    }

    [Test]
    public async Task RebuildForLibraryAsync_ShouldWriteOnlyMissingPairs_WhenOtherPairsAreUpToDate()
    {
        var movieId = await AddMovieWithFileAsync(_libraryId, "Inception");
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = _libraryId,
            MediaId = movieId
        });
        await _context.SaveChangesAsync();

        var recorder = new WriteCommandRecorder();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(recorder)
            .Options;
        await using var recordedContext = new ApplicationDbContext(options);
        var sut = new MediaLibraryAvailabilityService(
            recordedContext,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        await sut.RebuildForLibraryAsync(_libraryId);

        recorder.Commands.Should().BeEmpty();

        // A single new file must add its own pair without deleting or rewriting the rest of the library.
        var newMovieId = await AddMovieWithFileAsync(_libraryId, "Tenet");
        await sut.RebuildForLibraryAsync(_libraryId);

        recorder.Commands.Should().ContainSingle()
            .Which.Should().StartWith("INSERT INTO").And.EndWith("ON CONFLICT DO NOTHING");
        var mediaIds = await _context.MediaLibraryAvailabilities
            .Where(a => a.LibraryId == _libraryId)
            .Select(a => a.MediaId)
            .ToListAsync();
        mediaIds.Should().BeEquivalentTo([movieId, newMovieId]);
    }

    [Test]
    public async Task RebuildAllAsync_ShouldPopulatePairsForEveryLibrary()
    {
        var otherLibraryId = AddLibrary("Other movies");
        var movieId = await AddMovieWithFileAsync(_libraryId, "Inception");
        var otherMovieId = await AddMovieWithFileAsync(otherLibraryId, "Tenet");

        await _sut.RebuildAllAsync();

        var pairs = await _context.MediaLibraryAvailabilities
            .Select(a => new { a.LibraryId, a.MediaId })
            .ToListAsync();
        pairs.Should().BeEquivalentTo(
        [
            new { LibraryId = _libraryId, MediaId = movieId },
            new { LibraryId = otherLibraryId, MediaId = otherMovieId }
        ]);
    }

    private Guid AddLibrary(string title)
    {
        var libraryId = Guid.NewGuid();
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = _groupId,
            Title = title,
            MediaType = LibraryMediaType.Movie,
            RootPath = $"/media/{title}",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();
        return libraryId;
    }

    private async Task<Guid> AddMovieWithFileAsync(Guid libraryId, string title)
    {
        var movieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = title });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = movieId,
            Name = title,
            Extension = ".mkv",
            Path = $"/media/{libraryId}/{title}.mkv",
            Hash = 1,
            Size = 1
        });
        await _context.SaveChangesAsync();
        return movieId;
    }

    private sealed class WriteCommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
