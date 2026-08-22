using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class PlaybackBookmarkServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private PlaybackBookmarkService _sut = null!;
    private Guid _userId;
    private Guid _serieId;
    private Guid _seasonId;
    private Guid _episode1Id;
    private Guid _episode2Id;
    private Guid _episode3Id;

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
        _seasonId = season.Id;
        serie.Seasons.Add(season);

        _episode1Id = Guid.NewGuid();
        _episode2Id = Guid.NewGuid();
        _episode3Id = Guid.NewGuid();
        var episode1 = CreateEpisode(_episode1Id, serie, season, 1);
        var episode2 = CreateEpisode(_episode2Id, serie, season, 2);
        var episode3 = CreateEpisode(_episode3Id, serie, season, 3);

        _context.Medias.AddRange(serie, season, episode1, episode2, episode3);
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.AddRange(
            RemoteIndexedFilesSamples.Create(_episode1Id, libraryId, peerServerId),
            RemoteIndexedFilesSamples.Create(_episode2Id, libraryId, peerServerId),
            RemoteIndexedFilesSamples.Create(_episode3Id, libraryId, peerServerId));
        _context.SaveChanges();

        _sut = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task OnEpisodeCompletedAsync_ShouldCreateSeriesBookmarkWithNextEpisode()
    {
        var timeNow = DateTime.UtcNow;
        var policy = new VideoPlaybackPolicySettingsDto { MinResumePercent = 5 };

        await _sut.OnEpisodeCompletedAsync(_userId, sharedProfileId: null, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var seriesBookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId && b.SerieId == _serieId);

        seriesBookmark.NextEpisodeId.Should().Be(_episode2Id);
        seriesBookmark.LastCompletedEpisodeId.Should().Be(_episode1Id);
        _sut.IsSeriesBookmarkEligible(seriesBookmark, policy, timeNow, isNextPlayable: true).Should().BeTrue();
    }

    [Test]
    public async Task UpsertItemBookmarkAsync_ShouldBeEligibleForKeepWatching()
    {
        var timeNow = DateTime.UtcNow;
        var policy = new VideoPlaybackPolicySettingsDto { MinResumePercent = 5 };

        await _sut.UpsertItemBookmarkAsync(_userId, null, _episode2Id, 600, 3600, timeNow);
        await _context.SaveChangesAsync();

        var bookmark = await _context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId && b.MediaId == _episode2Id);

        _sut.MeetsItemResumeThreshold(bookmark, policy).Should().BeTrue();
        ContinueWatchingEligibility.IsItemBookmarkEligible(bookmark, policy, timeNow).Should().BeTrue();
    }

    [Test]
    public async Task DismissAsync_ShouldRemoveSeriesAndEpisodeBookmarks()
    {
        var timeNow = DateTime.UtcNow;
        await _sut.OnEpisodeCompletedAsync(_userId, null, _episode1Id, timeNow);
        await _sut.UpsertItemBookmarkAsync(_userId, null, _episode2Id, 100, 3600, timeNow);
        await _context.SaveChangesAsync();

        await _sut.DismissAsync(_episode2Id, _userId);
        await _context.SaveChangesAsync();

        (await _context.PlaybackBookmarks.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task OnEpisodeCompletedAsync_ShouldPointToFirstPlayableNext_WhenSeveralEpisodesExist()
    {
        var timeNow = DateTime.UtcNow;

        await _sut.OnEpisodeCompletedAsync(_userId, null, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var seriesBookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId && b.SerieId == _serieId);

        seriesBookmark.NextEpisodeId.Should().Be(_episode2Id);
        seriesBookmark.NextEpisodeId.Should().NotBe(_episode3Id);
    }

    [Test]
    public async Task ExpireStaleSeriesBookmarksAsync_ShouldRemove_WhenNextNeverStartedPastWindow()
    {
        var availableAt = DateTime.UtcNow.AddDays(-30);
        _context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = _userId,
            SerieId = _serieId,
            LastCompletedEpisodeId = _episode1Id,
            NextEpisodeId = _episode2Id,
            ActivityAt = DateTime.UtcNow.AddDays(-100),
            NextEpisodeAvailableAt = availableAt,
            UpdatedAt = availableAt
        });
        await _context.SaveChangesAsync();

        var policy = new VideoPlaybackPolicySettingsDto { ContinueWatchingMaxAgeDays = 14 };
        await _sut.ExpireStaleSeriesBookmarksAsync(_userId, null, policy, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        (await _context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task ExpireStaleSeriesBookmarksAsync_ShouldKeep_WhenUserStartedNextEpisode()
    {
        var availableAt = DateTime.UtcNow.AddDays(-30);
        _context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = _userId,
            SerieId = _serieId,
            LastCompletedEpisodeId = _episode1Id,
            NextEpisodeId = _episode2Id,
            ActivityAt = DateTime.UtcNow.AddDays(-100),
            NextEpisodeAvailableAt = availableAt,
            UpdatedAt = availableAt
        });
        await _sut.UpsertItemBookmarkAsync(_userId, null, _episode2Id, 120, 3600, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        var policy = new VideoPlaybackPolicySettingsDto { ContinueWatchingMaxAgeDays = 14 };
        await _sut.ExpireStaleSeriesBookmarksAsync(_userId, null, policy, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        (await _context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task RefreshSeriesBookmarksForSerieAsync_ShouldSetNext_WhenLateSeasonAppears()
    {
        var oldActivity = DateTime.UtcNow.AddDays(-400);
        _context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = _userId,
            SerieId = _serieId,
            LastCompletedEpisodeId = _episode1Id,
            NextEpisodeId = null,
            ActivityAt = oldActivity,
            NextEpisodeAvailableAt = default,
            UpdatedAt = oldActivity
        });
        await _context.SaveChangesAsync();

        var timeNow = DateTime.UtcNow;
        await _sut.RefreshSeriesBookmarksForSerieAsync(_serieId, timeNow);
        await _context.SaveChangesAsync();

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId);

        bookmark.NextEpisodeId.Should().Be(_episode2Id);
        bookmark.NextEpisodeAvailableAt.Should().Be(timeNow);
        bookmark.ActivityAt.Should().Be(oldActivity);

        var policy = new VideoPlaybackPolicySettingsDto { ContinueWatchingMaxAgeDays = 14 };
        _sut.IsSeriesBookmarkEligible(bookmark, policy, timeNow, isNextPlayable: true).Should().BeTrue();
    }

    [Test]
    public async Task BackfillMissingNextEpisodesAsync_ShouldFillNextEpisodeId()
    {
        _context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
        {
            UserId = _userId,
            SerieId = _serieId,
            LastCompletedEpisodeId = _episode1Id,
            NextEpisodeId = null,
            ActivityAt = DateTime.UtcNow.AddDays(-10),
            NextEpisodeAvailableAt = default,
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });
        await _context.SaveChangesAsync();

        var timeNow = DateTime.UtcNow;
        await _sut.BackfillMissingNextEpisodesAsync(_userId, null, timeNow);
        await _context.SaveChangesAsync();

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId);

        bookmark.NextEpisodeId.Should().Be(_episode2Id);
        bookmark.NextEpisodeAvailableAt.Should().Be(timeNow);
    }

    [Test]
    public async Task OnEpisodeCompletedAsync_ShouldWorkForSharedProfile()
    {
        var sharedProfileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        var timeNow = DateTime.UtcNow;
        await _sut.OnEpisodeCompletedAsync(userId: null, sharedProfileId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.SharedProfileId == sharedProfileId);

        bookmark.NextEpisodeId.Should().Be(_episode2Id);
        bookmark.UserId.Should().BeNull();
    }

    [Test]
    public async Task DismissForSharedProfileAsync_ShouldRemoveSharedSeriesBookmark()
    {
        var sharedProfileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        await _sut.OnEpisodeCompletedAsync(null, sharedProfileId, _episode1Id, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _sut.DismissForSharedProfileAsync(_episode2Id, sharedProfileId);
        await _context.SaveChangesAsync();

        (await _context.PlaybackBookmarks.CountAsync()).Should().Be(0);
    }

    private static SerieEpisode CreateEpisode(Guid id, Serie serie, SerieSeason season, int number) =>
        new()
        {
            Id = id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = number,
            Title = $"E{number}",
            SortTitle = $"E{number}"
        };
}
