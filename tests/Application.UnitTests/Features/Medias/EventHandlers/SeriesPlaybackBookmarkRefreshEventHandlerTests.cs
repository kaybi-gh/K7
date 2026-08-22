using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.EventHandlers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.Medias.EventHandlers;

[TestFixture]
public class SeriesPlaybackBookmarkRefreshEventHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private SeriesPlaybackBookmarkRefreshEventHandler _handler = null!;
    private Guid _userId;
    private Guid _serieId;
    private Guid _episode1Id;
    private Guid _episode2Id;

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

        _userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        _serieId = serie.Id;
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);

        _episode1Id = Guid.NewGuid();
        _episode2Id = Guid.NewGuid();
        var episode1 = new SerieEpisode
        {
            Id = _episode1Id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "E1",
            SortTitle = "E1"
        };
        var episode2 = new SerieEpisode
        {
            Id = _episode2Id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 2,
            Title = "E2",
            SortTitle = "E2"
        };

        _context.Medias.AddRange(serie, season, episode1, episode2);
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.Add(RemoteIndexedFilesSamples.Create(_episode2Id, libraryId, peerServerId));
        _context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = _userId,
            SerieId = _serieId,
            LastCompletedEpisodeId = _episode1Id,
            ActivityAt = DateTime.UtcNow.AddDays(-2),
            NextEpisodeAvailableAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        });
        _context.SaveChanges();

        _handler = new SeriesPlaybackBookmarkRefreshEventHandler(
            new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance),
            _context,
            NullLogger<SeriesPlaybackBookmarkRefreshEventHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldRefreshSeriesBookmarkNextEpisode_WhenEpisodeIsCreated()
    {
        var episode2 = await _context.Medias.OfType<SerieEpisode>().SingleAsync(e => e.Id == _episode2Id);

        await _handler.Handle(new MediaCreatedEvent(episode2), CancellationToken.None);

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId && b.SerieId == _serieId);

        bookmark.NextEpisodeId.Should().Be(_episode2Id);
    }

    [Test]
    public async Task Handle_ShouldIgnoreNonEpisodeMedia()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Film", SortTitle = "Film" };

        await _handler.Handle(new MediaCreatedEvent(movie), CancellationToken.None);

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId);
        bookmark.NextEpisodeId.Should().BeNull();
    }
}
