using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class StartupLottiePlaybackTests
{
    [Test]
    public void ReadyDelayMs_ShouldReturnFullDuration_WhenDurationIsKnown()
    {
        StartupLottiePlayback.ReadyDelayMs(3000, hasAnimation: true).Should().Be(3000);
    }

    [Test]
    public void ReadyDelayMs_ShouldFallBack_WhenDurationIsMissing()
    {
        StartupLottiePlayback.ReadyDelayMs(0, hasAnimation: true).Should().Be(StartupLottiePlayback.ReadyFallbackMs);
    }

    [Test]
    public void ReadyDelayMs_ShouldBeShort_WhenAnimationMissing()
    {
        StartupLottiePlayback.ReadyDelayMs(3000, hasAnimation: false)
            .Should().Be(StartupLottiePlayback.NoAnimationReadyMs);
    }

    [Test]
    public void ShouldHideStaticLogo_ShouldWaitUntilRevealHasOpacity()
    {
        StartupLottiePlayback.ShouldHideStaticLogo(hasAnimation: true, elapsedMs: 0).Should().BeFalse();
        StartupLottiePlayback.ShouldHideStaticLogo(hasAnimation: true, elapsedMs: 399).Should().BeFalse();
        StartupLottiePlayback.ShouldHideStaticLogo(hasAnimation: true, elapsedMs: 400).Should().BeTrue();
    }

    [Test]
    public void ShouldHideStaticLogo_ShouldKeepMark_WhenAnimationFailedToLoad()
    {
        StartupLottiePlayback.ShouldHideStaticLogo(hasAnimation: false, elapsedMs: 1000).Should().BeFalse();
    }

    [Test]
    public void ShouldAssignStartPageOnTimeout_ShouldSkip_WhenOverlayAlreadyAssigned()
    {
        StartupLottiePlayback.ShouldAssignStartPageOnTimeout(
            overlayShown: true,
            startPageAssigned: true).Should().BeFalse();
    }

    [Test]
    public void ShouldAssignStartPageOnTimeout_ShouldFire_WhenOverlayShownButNotAssigned()
    {
        StartupLottiePlayback.ShouldAssignStartPageOnTimeout(
            overlayShown: true,
            startPageAssigned: false).Should().BeTrue();
    }

    [Test]
    public void ShouldAssignStartPageOnTimeout_ShouldFire_WhenOverlayWasNotShown()
    {
        StartupLottiePlayback.ShouldAssignStartPageOnTimeout(
            overlayShown: false,
            startPageAssigned: false).Should().BeTrue();
    }

    [Test]
    public void TryGetFitRect_ShouldLetterbox_WhenCanvasIsSquare()
    {
        var fitted = StartupLottiePlayback.TryGetFitRect(
            220,
            220,
            128,
            70,
            out var left,
            out var top,
            out var right,
            out var bottom);

        fitted.Should().BeTrue();
        left.Should().Be(0);
        right.Should().Be(220);
        (bottom - top).Should().BeApproximately(220f * 70f / 128f, 0.01f);
        top.Should().BeGreaterThan(0);
    }
}
