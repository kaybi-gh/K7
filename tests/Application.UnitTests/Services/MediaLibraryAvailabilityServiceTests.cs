using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
}
