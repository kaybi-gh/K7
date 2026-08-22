using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Services;

public class ContinueWatchingEligibilityTests
{
    private static readonly VideoPlaybackPolicySettingsDto DefaultPolicy = new();

    [Test]
    public void MeetsItemResumeThreshold_ShouldReturnTrue_WhenAboveThreshold()
    {
        var bookmark = CreateItemBookmark(progressPercent: 10);

        ContinueWatchingEligibility.MeetsItemResumeThreshold(bookmark, DefaultPolicy).Should().BeTrue();
    }

    [Test]
    public void IsItemBookmarkEligible_ShouldReturnFalse_WhenOutsideWindow()
    {
        var bookmark = CreateItemBookmark(
            progressPercent: 10,
            updatedAt: DateTime.UtcNow.AddDays(-DefaultPolicy.ContinueWatchingMaxAgeDays - 1));

        ContinueWatchingEligibility.IsItemBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow)
            .Should().BeFalse();
    }

    [Test]
    public void IsItemBookmarkEligible_ShouldReturnTrue_WhenInsideWindowAndAboveThreshold()
    {
        var bookmark = CreateItemBookmark(progressPercent: 10, updatedAt: DateTime.UtcNow.AddDays(-3));

        ContinueWatchingEligibility.IsItemBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow)
            .Should().BeTrue();
    }

    [Test]
    public void IsItemBookmarkEligible_ShouldReturnTrue_WhenEarlySerieEpisodeWatch()
    {
        var bookmark = new ItemPlaybackBookmark
        {
            PositionSeconds = 48,
            DurationSeconds = 3600,
            UpdatedAt = DateTime.UtcNow
        };

        ContinueWatchingEligibility.IsItemBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow, isSerieEpisode: true)
            .Should().BeTrue();
    }

    [Test]
    public void IsSeriesBookmarkEligible_ShouldReturnTrue_WhenNextPlayableAndWithinWindow()
    {
        var bookmark = new SeriesPlaybackBookmark
        {
            NextEpisodeId = Guid.NewGuid(),
            NextEpisodeAvailableAt = DateTime.UtcNow.AddDays(-3)
        };

        ContinueWatchingEligibility.IsSeriesBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow, isNextPlayable: true)
            .Should().BeTrue();
    }

    [Test]
    public void IsSeriesBookmarkEligible_ShouldReturnTrue_WhenNextPlayableEvenIfActivityIsOld()
    {
        var bookmark = new SeriesPlaybackBookmark
        {
            NextEpisodeId = Guid.NewGuid(),
            ActivityAt = DateTime.UtcNow.AddDays(-400),
            NextEpisodeAvailableAt = DateTime.UtcNow.AddDays(-3)
        };

        ContinueWatchingEligibility.IsSeriesBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow, isNextPlayable: true)
            .Should().BeTrue();
    }

    [Test]
    public void IsSeriesBookmarkEligible_ShouldReturnFalse_WhenNextAvailableOutsideWindow()
    {
        var bookmark = new SeriesPlaybackBookmark
        {
            NextEpisodeId = Guid.NewGuid(),
            ActivityAt = DateTime.UtcNow.AddDays(-3),
            NextEpisodeAvailableAt = DateTime.UtcNow.AddDays(-DefaultPolicy.ContinueWatchingMaxAgeDays - 1)
        };

        ContinueWatchingEligibility.IsSeriesBookmarkEligible(bookmark, DefaultPolicy, DateTime.UtcNow, isNextPlayable: true)
            .Should().BeFalse();
    }

    [Test]
    public void GetWindowCutoff_ShouldReturnNull_WhenMaxAgeIsZero()
    {
        var policy = DefaultPolicy with { ContinueWatchingMaxAgeDays = 0 };

        ContinueWatchingEligibility.GetWindowCutoff(policy, DateTime.UtcNow).Should().BeNull();
    }

    [Test]
    public void MeetsItemResumeThreshold_ShouldReturnFalse_WhenDurationBelowMinResumeDuration()
    {
        var bookmark = CreateItemBookmark(progressPercent: 35, durationSeconds: 60);

        ContinueWatchingEligibility.MeetsItemResumeThreshold(bookmark, DefaultPolicy).Should().BeFalse();
    }


    private static ItemPlaybackBookmark CreateItemBookmark(
        double progressPercent = 0,
        double durationSeconds = 3600,
        DateTime? updatedAt = null)
    {
        var position = durationSeconds * progressPercent / 100.0;
        return new ItemPlaybackBookmark
        {
            PositionSeconds = position,
            DurationSeconds = durationSeconds,
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
    }
}
