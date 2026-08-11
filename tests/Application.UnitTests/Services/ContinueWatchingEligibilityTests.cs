using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Services;

public class ContinueWatchingEligibilityTests
{
    private static readonly VideoPlaybackPolicySettingsDto DefaultPolicy = new();

    [Test]
    public void MeetsResumeThreshold_ShouldReturnTrue_WhenExcludedButAboveThreshold()
    {
        var state = CreateState(excluded: true, progress: 10);

        ContinueWatchingEligibility.MeetsResumeThreshold(state, DefaultPolicy).Should().BeTrue();
    }

    [Test]
    public void MeetsThreshold_ShouldReturnFalse_WhenExcludedEvenIfAboveThreshold()
    {
        var state = CreateState(excluded: true, progress: 10);
        var utcNow = DateTime.UtcNow;

        ContinueWatchingEligibility.MeetsThreshold(state, DefaultPolicy, utcNow).Should().BeFalse();
    }

    [Test]
    public void MeetsThreshold_ShouldReturnFalse_WhenOutsideWindow()
    {
        var state = CreateState(
            progress: 10,
            lastInteractedAt: DateTime.UtcNow.AddDays(-DefaultPolicy.ContinueWatchingMaxAgeDays - 1));

        ContinueWatchingEligibility.MeetsThreshold(state, DefaultPolicy, DateTime.UtcNow).Should().BeFalse();
    }

    [Test]
    public void MeetsThreshold_ShouldReturnTrue_WhenInsideWindowAndAboveThreshold()
    {
        var state = CreateState(progress: 10, lastInteractedAt: DateTime.UtcNow.AddDays(-3));

        ContinueWatchingEligibility.MeetsThreshold(state, DefaultPolicy, DateTime.UtcNow).Should().BeTrue();
    }

    [Test]
    public void MeetsThreshold_ShouldReturnTrue_WhenNextEpisodePlaceholderAtZeroProgress()
    {
        var state = CreateState(progress: 0, lastInteractedAt: DateTime.UtcNow);
        state.LastPlaybackPosition = 0;
        state.PlayCount = 0;

        ContinueWatchingEligibility.IsContinueWatchingPlaceholder(state).Should().BeTrue();
        ContinueWatchingEligibility.MeetsResumeThreshold(state, DefaultPolicy).Should().BeFalse();
        ContinueWatchingEligibility.MeetsThreshold(state, DefaultPolicy, DateTime.UtcNow).Should().BeTrue();
    }

    [Test]
    public void MeetsThreshold_ShouldReturnFalse_WhenZeroProgressButExcluded()
    {
        var state = CreateState(excluded: true, progress: 0, lastInteractedAt: DateTime.UtcNow);
        state.LastPlaybackPosition = 0;
        state.PlayCount = 0;

        ContinueWatchingEligibility.MeetsThreshold(state, DefaultPolicy, DateTime.UtcNow).Should().BeFalse();
    }

    [Test]
    public void GetWindowCutoff_ShouldReturnNull_WhenMaxAgeIsZero()
    {
        var policy = DefaultPolicy with { ContinueWatchingMaxAgeDays = 0 };

        ContinueWatchingEligibility.GetWindowCutoff(policy, DateTime.UtcNow).Should().BeNull();
    }

    [Test]
    public void MeetsResumeThreshold_ShouldReturnFalse_WhenDurationBelowMinResumeDuration()
    {
        var state = CreateState(progress: 35, lastKnownDurationSeconds: 60);

        ContinueWatchingEligibility.MeetsResumeThreshold(state, DefaultPolicy).Should().BeFalse();
    }

    [Test]
    public void MeetsResumeThreshold_ShouldReturnTrue_WhenDurationUnknownEvenIfBelowMinWouldApply()
    {
        // Unknown runtime (0) must not exclude a title that clears MinResumePercent.
        var state = CreateState(progress: 35, lastKnownDurationSeconds: 0);

        ContinueWatchingEligibility.MeetsResumeThreshold(state, DefaultPolicy).Should().BeTrue();
    }

    [Test]
    public void MeetsResumeThreshold_ShouldReturnFalse_WhenProgressBelowPercent()
    {
        var state = CreateState(progress: 2, lastKnownDurationSeconds: 3600);

        ContinueWatchingEligibility.MeetsResumeThreshold(state, DefaultPolicy).Should().BeFalse();
    }

    private static UserMediaState CreateState(
        bool excluded = false,
        double progress = 0,
        DateTime? lastInteractedAt = null,
        double lastKnownDurationSeconds = 3600)
    {
        return new UserMediaState
        {
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            ExcludedFromContinueWatching = excluded,
            ProgressPercentage = progress,
            LastKnownDurationSeconds = lastKnownDurationSeconds,
            LastInteractedAt = lastInteractedAt ?? DateTime.UtcNow,
            IsCompleted = false
        };
    }
}
