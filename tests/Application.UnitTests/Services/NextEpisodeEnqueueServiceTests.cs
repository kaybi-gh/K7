using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class NextEpisodeEnqueueServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private NextEpisodeEnqueueService _sut = null!;
    private Guid _userId;
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
        _context.SaveChanges();

        _sut = new NextEpisodeEnqueueService(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task EnqueueNextEpisodeAsync_ShouldCreateZeroProgressPlaceholder_EligibleForKeepWatching()
    {
        var timeNow = DateTime.UtcNow;
        var policy = new VideoPlaybackPolicySettingsDto { MinResumePercent = 5 };

        await _sut.EnqueueNextEpisodeAsync(_userId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(0);
        next.LastPlaybackPosition.Should().Be(0);
        next.PlayCount.Should().Be(0);
        next.IsCompleted.Should().BeFalse();
        next.LastInteractedAt.Should().Be(timeNow);
        next.ExcludedFromContinueWatching.Should().BeFalse();

        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
        ContinueWatchingEligibility.MeetsResumeThreshold(next, policy).Should().BeFalse();
        ContinueWatchingEligibility.MeetsThreshold(next, policy, timeNow).Should().BeTrue();
    }

    [Test]
    public async Task EnqueueNextEpisodeAsync_ShouldNotOverwriteRealProgress()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episode2Id,
            ProgressPercentage = 40,
            LastPlaybackPosition = 1200,
            IsCompleted = false,
            PlayCount = 0,
            LastInteractedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var timeNow = DateTime.UtcNow;
        await _sut.EnqueueNextEpisodeAsync(_userId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(40);
        next.LastPlaybackPosition.Should().Be(1200);
        next.LastInteractedAt.Should().Be(timeNow);
    }

    [Test]
    public async Task EnqueueNextEpisodeForSharedProfileAsync_ShouldCreateZeroProgressPlaceholder()
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
        await _sut.EnqueueNextEpisodeForSharedProfileAsync(sharedProfileId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var next = await _context.SharedProfileMediaStates
            .SingleAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(0);
        next.LastPlaybackPosition.Should().Be(0);
        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
    }

    [Test]
    public async Task EnqueueNextEpisodeAsync_ShouldSkipCompletedNext_AndPlaceholderFollowingEpisode()
    {
        var episode3Id = Guid.NewGuid();
        var episode2 = await _context.Medias.OfType<SerieEpisode>().SingleAsync(e => e.Id == _episode2Id);
        var episode3 = new SerieEpisode
        {
            Id = episode3Id,
            SerieId = episode2.SerieId,
            Serie = episode2.Serie,
            SeasonId = episode2.SeasonId,
            Season = episode2.Season,
            EpisodeNumber = 3,
            Title = "E3",
            SortTitle = "E3"
        };
        _context.Medias.Add(episode3);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episode2Id,
            IsCompleted = true,
            ProgressPercentage = 100,
            LastInteractedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var timeNow = DateTime.UtcNow;
        await _sut.EnqueueNextEpisodeAsync(_userId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == episode3Id);

        next.ProgressPercentage.Should().Be(0);
        next.PlayCount.Should().Be(0);
        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
    }
}
