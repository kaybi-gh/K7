using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PlaybackStartRecoveryPolicyTests
{
    [Test]
    public void ShouldStayOnRemux_ShouldBeTrue_OnColdStart()
    {
        var now = DateTime.UtcNow;
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: true,
                isHls: true,
                remuxReloadsDone: 0,
                lastRemuxRetryUtc: DateTime.MinValue,
                utcNow: now)
            .Should().BeTrue();
        PlaybackStartRecoveryPolicy.ShouldReloadRemuxSource(0).Should().BeFalse();
    }

    [Test]
    public void ShouldStayOnRemux_ShouldHoldThroughInterval_AfterReloads()
    {
        var last = DateTime.UtcNow;
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: true,
                isHls: true,
                remuxReloadsDone: 2,
                lastRemuxRetryUtc: last,
                utcNow: last.AddSeconds(10))
            .Should().BeTrue();
        PlaybackStartRecoveryPolicy.ShouldReloadRemuxSource(2).Should().BeFalse();
    }

    [Test]
    public void ShouldStayOnRemux_ShouldBeFalse_AfterInterval()
    {
        var last = DateTime.UtcNow;
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: true,
                isHls: true,
                remuxReloadsDone: 2,
                lastRemuxRetryUtc: last,
                utcNow: last.AddSeconds(26))
            .Should().BeFalse();
    }

    [Test]
    public void ShouldStayOnRemux_ShouldBeFalse_WhenNotHlsOriginal()
    {
        var now = DateTime.UtcNow;
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: true,
                isHls: false,
                remuxReloadsDone: 0,
                lastRemuxRetryUtc: DateTime.MinValue,
                utcNow: now)
            .Should().BeFalse();
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: false,
                isHls: true,
                remuxReloadsDone: 0,
                lastRemuxRetryUtc: DateTime.MinValue,
                utcNow: now)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldReloadRemuxSource_ShouldBeFalse_ForAnyReloadCount()
    {
        PlaybackStartRecoveryPolicy.ShouldReloadRemuxSource(0).Should().BeFalse();
        PlaybackStartRecoveryPolicy.ShouldReloadRemuxSource(1).Should().BeFalse();
        PlaybackStartRecoveryPolicy.ShouldReloadRemuxSource(2).Should().BeFalse();
    }

    [Test]
    public void ShouldStayOnRemux_ShouldIgnoreReloadCount_OnColdStart()
    {
        var now = DateTime.UtcNow;
        PlaybackStartRecoveryPolicy.ShouldStayOnRemux(
                isOriginalQuality: true,
                isHls: true,
                remuxReloadsDone: 99,
                lastRemuxRetryUtc: DateTime.MinValue,
                utcNow: now)
            .Should().BeTrue();
    }
}
