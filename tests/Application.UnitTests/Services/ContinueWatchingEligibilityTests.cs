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
    public void GetWindowCutoff_ShouldReturnNull_WhenMaxAgeIsZero()
    {
        var policy = DefaultPolicy with { ContinueWatchingMaxAgeDays = 0 };

        ContinueWatchingEligibility.GetWindowCutoff(policy, DateTime.UtcNow).Should().BeNull();
    }

    private static UserMediaState CreateState(
        bool excluded = false,
        double progress = 0,
        DateTime? lastInteractedAt = null)
    {
        return new UserMediaState
        {
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            ExcludedFromContinueWatching = excluded,
            ProgressPercentage = progress,
            LastKnownDurationSeconds = 3600,
            LastInteractedAt = lastInteractedAt ?? DateTime.UtcNow,
            IsCompleted = false
        };
    }
}
