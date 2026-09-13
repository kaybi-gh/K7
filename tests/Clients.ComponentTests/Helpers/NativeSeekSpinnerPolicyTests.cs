using AwesomeAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeSeekSpinnerPolicyTests
{
    [Test]
    public void ShouldArmStartupVeil_ShouldBeFalse_ForSeekSpinnerOnly()
    {
        NativeSeekSpinnerPolicy.ShouldArmStartupVeil(dimBackground: false).Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldArmStartupVeil(dimBackground: true).Should().BeTrue();
    }

    [Test]
    public void ShouldLiftVeilOnPlaying_ShouldBeTrue_OnlyForVideoJsStartup()
    {
        NativeSeekSpinnerPolicy.ShouldLiftVeilOnPlaying(
                awaitingFirstFrame: true,
                seekSpinnerActive: false,
                decoderOwnsFirstFrame: false)
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldLiftVeilOnPlaying(
                awaitingFirstFrame: true,
                seekSpinnerActive: false,
                decoderOwnsFirstFrame: true)
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldLiftVeilOnPlaying(
                awaitingFirstFrame: true,
                seekSpinnerActive: true,
                decoderOwnsFirstFrame: false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldHoldUntilDecoderReachesSeek_ShouldBeTrue_ForOriginalHls()
    {
        NativeSeekSpinnerPolicy.ShouldHoldUntilDecoderReachesSeek(
                isOriginalQuality: true,
                isHls: true)
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldHoldUntilDecoderReachesSeek(
                isOriginalQuality: false,
                isHls: true)
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHoldUntilDecoderReachesSeek(
                isOriginalQuality: true,
                isHls: false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldHideOnPlaying_ShouldSkipRemuxAudioSeek()
    {
        NativeSeekSpinnerPolicy.ShouldHideOnPlaying(
                isWindows: true,
                sawSeekBuffering: true,
                holdUntilDecoderReachesSeek: true)
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHideOnPlaying(
                isWindows: true,
                sawSeekBuffering: true,
                holdUntilDecoderReachesSeek: false)
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldHideOnPlaying(
                isWindows: false,
                sawSeekBuffering: true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldShowOnMidPlayBuffering_ShouldBeTrue_OnWindowsAfterFirstFrame()
    {
        NativeSeekSpinnerPolicy.ShouldShowOnMidPlayBuffering(isWindows: true, awaitingFirstFrame: false)
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldShowOnMidPlayBuffering(isWindows: true, awaitingFirstFrame: true)
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldShowOnMidPlayBuffering(isWindows: false, awaitingFirstFrame: false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldHoldStartupFirstFrameForSeek_ShouldIgnoreStaleTarget()
    {
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: true,
                hasSeekTarget: true,
                sinceSeek: TimeSpan.FromSeconds(1))
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: true,
                hasSeekTarget: true,
                sinceSeek: TimeSpan.FromSeconds(20))
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: true,
                hasSeekTarget: false,
                sinceSeek: TimeSpan.FromSeconds(1))
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: false,
                hasSeekTarget: true,
                sinceSeek: TimeSpan.FromSeconds(1))
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: false,
                isHls: true,
                hasSeekTarget: true,
                sinceSeek: TimeSpan.FromSeconds(1))
            .Should().BeFalse();
    }

    [Test]
    public void ShouldAcceptSeekFirstFrame_ShouldWaitUntilDecoderNearTarget()
    {
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(120, 400).Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(399, 400).Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(0, 400).Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(10, -1).Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(397.5, 400).Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldAcceptSeekFirstFrame(397.4, 400).Should().BeFalse();
    }

    [Test]
    public void ShouldLiftVeilOnPlaying_ShouldBeFalse_WhenNotAwaitingFirstFrame()
    {
        NativeSeekSpinnerPolicy.ShouldLiftVeilOnPlaying(
                awaitingFirstFrame: false,
                seekSpinnerActive: false,
                decoderOwnsFirstFrame: false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldHoldStartupFirstFrameForSeek_ShouldBeFalse_WhenSeekTimeIsInvalid()
    {
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: true,
                hasSeekTarget: true,
                sinceSeek: TimeSpan.FromSeconds(-1))
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHoldStartupFirstFrameForSeek(
                isOriginalQuality: true,
                isHls: true,
                hasSeekTarget: true,
                sinceSeek: NativeSeekSpinnerPolicy.SeekFirstFrameHoldWindow)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldHideAfterWindowsInstantSeek_ShouldBeTrue_OnlyWhenPlaying()
    {
        NativeSeekSpinnerPolicy.ShouldHideAfterWindowsInstantSeek(PlaybackState.Playing)
            .Should().BeTrue();
        NativeSeekSpinnerPolicy.ShouldHideAfterWindowsInstantSeek(PlaybackState.Buffering)
            .Should().BeFalse();
        NativeSeekSpinnerPolicy.ShouldHideAfterWindowsInstantSeek(PlaybackState.Paused)
            .Should().BeFalse();
    }
}
