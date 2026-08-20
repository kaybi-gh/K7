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

    [Test]
    public async Task EnqueueWatchersForNewEpisodeAsync_ShouldPlaceholderUsersWhoCompletedPrevious()
    {
        var finishedAt = DateTime.UtcNow.AddDays(-3);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episode1Id,
            IsCompleted = true,
            ProgressPercentage = 100,
            LastInteractedAt = finishedAt
        });
        await _context.SaveChangesAsync();

        await _sut.EnqueueWatchersForNewEpisodeAsync(_episode2Id);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(0);
        next.PlayCount.Should().Be(0);
        next.IsCompleted.Should().BeFalse();
        next.LastInteractedAt.Should().Be(finishedAt);
        next.ExcludedFromContinueWatching.Should().BeFalse();
        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
    }

    [Test]
    public async Task EnqueueWatchersForNewEpisodeAsync_ShouldSkipUsersWhoDidNotCompletePrevious()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episode1Id,
            IsCompleted = false,
            ProgressPercentage = 40,
            LastInteractedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _sut.EnqueueWatchersForNewEpisodeAsync(_episode2Id);
        await _context.SaveChangesAsync();

        var states = await _context.UserMediaStates
            .Where(s => s.UserId == _userId && s.MediaId == _episode2Id)
            .ToListAsync();

        states.Should().BeEmpty();
    }

    [Test]
    public async Task EnqueueWatchersForNewEpisodeAsync_ShouldNotOverwriteExistingState()
    {
        var finishedAt = DateTime.UtcNow.AddDays(-2);
        _context.UserMediaStates.AddRange(
            new UserMediaState
            {
                UserId = _userId,
                MediaId = _episode1Id,
                IsCompleted = true,
                ProgressPercentage = 100,
                LastInteractedAt = finishedAt
            },
            new UserMediaState
            {
                UserId = _userId,
                MediaId = _episode2Id,
                IsCompleted = false,
                ProgressPercentage = 25,
                LastPlaybackPosition = 400,
                PlayCount = 1,
                LastInteractedAt = DateTime.UtcNow.AddHours(-1),
                ExcludedFromContinueWatching = true
            });
        await _context.SaveChangesAsync();

        await _sut.EnqueueWatchersForNewEpisodeAsync(_episode2Id);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(25);
        next.LastPlaybackPosition.Should().Be(400);
        next.ExcludedFromContinueWatching.Should().BeTrue();
        next.LastInteractedAt.Should().NotBe(finishedAt);
    }

    [Test]
    public async Task EnqueueWatchersForNewEpisodeAsync_ShouldPlaceholderSharedProfilesWhoCompletedPrevious()
    {
        var sharedProfileId = Guid.NewGuid();
        var finishedAt = DateTime.UtcNow.AddDays(-1);
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = sharedProfileId,
            MediaId = _episode1Id,
            IsCompleted = true,
            ProgressPercentage = 100,
            LastInteractedAt = finishedAt
        });
        await _context.SaveChangesAsync();

        await _sut.EnqueueWatchersForNewEpisodeAsync(_episode2Id);
        await _context.SaveChangesAsync();

        var next = await _context.SharedProfileMediaStates
            .SingleAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == _episode2Id);

        next.ProgressPercentage.Should().Be(0);
        next.LastInteractedAt.Should().Be(finishedAt);
        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
    }

    [Test]
    public async Task EnqueueWatchersForNewEpisodeAsync_ShouldUseLastEpisodeOfPreviousSeason()
    {
        var episode1 = await _context.Medias.OfType<SerieEpisode>().SingleAsync(e => e.Id == _episode1Id);
        var season2 = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = episode1.SerieId,
            Serie = episode1.Serie,
            SeasonNumber = 2,
            Title = "Season 2",
            SortTitle = "Season 2"
        };
        var s2e1Id = Guid.NewGuid();
        var s2e1 = new SerieEpisode
        {
            Id = s2e1Id,
            SerieId = episode1.SerieId,
            Serie = episode1.Serie,
            SeasonId = season2.Id,
            Season = season2,
            EpisodeNumber = 1,
            Title = "S2E1",
            SortTitle = "S2E1"
        };
        _context.Medias.AddRange(season2, s2e1);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episode2Id,
            IsCompleted = true,
            ProgressPercentage = 100,
            LastInteractedAt = DateTime.UtcNow.AddDays(-7)
        });
        await _context.SaveChangesAsync();

        await _sut.EnqueueWatchersForNewEpisodeAsync(s2e1Id);
        await _context.SaveChangesAsync();

        var next = await _context.UserMediaStates
            .SingleAsync(s => s.UserId == _userId && s.MediaId == s2e1Id);

        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(next).Should().BeTrue();
    }
}
