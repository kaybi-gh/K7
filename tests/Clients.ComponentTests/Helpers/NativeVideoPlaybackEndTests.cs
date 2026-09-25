using AwesomeAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeVideoPlaybackEndTests
{
    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeFalse_WhenOpeningSource()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: true,
            isVisible: true,
            currentState: PlaybackState.Playing,
            durationSeconds: 100,
            positionSeconds: 100).Should().BeFalse();
    }

    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeFalse_WhenPlayerHidden()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: false,
            isVisible: false,
            currentState: PlaybackState.Playing,
            durationSeconds: 100,
            positionSeconds: 100).Should().BeFalse();
    }

    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeTrue_WhenAlreadyEnded()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: false,
            isVisible: true,
            currentState: PlaybackState.Ended,
            durationSeconds: 0,
            positionSeconds: 0).Should().BeTrue();
    }

    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeTrue_WhenPositionNearDuration()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: false,
            isVisible: true,
            currentState: PlaybackState.Playing,
            durationSeconds: 100,
            positionSeconds: 99).Should().BeTrue();
    }

    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeFalse_WhenStoppedMidPlayback()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: false,
            isVisible: true,
            currentState: PlaybackState.Playing,
            durationSeconds: 100,
            positionSeconds: 40).Should().BeFalse();
    }

    [Test]
    public void ShouldTreatStoppedAsEnded_ShouldBeFalse_WhenDurationTooShort()
    {
        NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
            isOpeningSource: false,
            isVisible: true,
            currentState: PlaybackState.Playing,
            durationSeconds: 3,
            positionSeconds: 3).Should().BeFalse();
    }

    [Test]
    public void IsSeekToMediaEnd_ShouldBeTrue_WhenTargetWithinSeekTolerance()
    {
        NativeVideoPlaybackEnd.IsSeekToMediaEnd(98.1, durationSeconds: 100).Should().BeTrue();
    }

    [Test]
    public void IsSeekToMediaEnd_ShouldBeFalse_WhenTargetIsEarlier()
    {
        NativeVideoPlaybackEnd.IsSeekToMediaEnd(90, durationSeconds: 100).Should().BeFalse();
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldStayPaused_WhenNotNearEnd()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Paused,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 40).Should().Be(PlaybackState.Paused);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldEnd_WhenPausedAtEof()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Paused,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 99.2).Should().Be(PlaybackState.Ended);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldEnd_WhenFrozenPlayingAtEof()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Playing,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 99.5).Should().Be(PlaybackState.Ended);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldKeepPlaying_WhenClockStillMovingAtEof()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Playing,
            engineIsPlaying: true,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 99.5).Should().Be(PlaybackState.Playing);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldKeepBuffering_WhenFetchingLastSegment()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Buffering,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 99.5).Should().Be(PlaybackState.Buffering);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldDemoteEnded_WhenOpeningSource()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Ended,
            engineIsPlaying: false,
            isOpeningSource: true,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 100).Should().Be(PlaybackState.Buffering);
    }

    [Test]
    public void PromoteIfMediaEnded_ShouldKeepEnded_WhenNotOpeningSource()
    {
        NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            PlaybackState.Ended,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: true,
            durationSeconds: 100,
            positionSeconds: 100).Should().Be(PlaybackState.Ended);
    }

    [Test]
    public void ShouldApplyPlaybackState_ShouldIgnorePauseAfterEnded()
    {
        NativeVideoPlaybackEnd.ShouldApplyPlaybackState(PlaybackState.Ended, PlaybackState.Paused)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldApplyPlaybackState_ShouldAllowPlayingAfterEnded()
    {
        NativeVideoPlaybackEnd.ShouldApplyPlaybackState(PlaybackState.Ended, PlaybackState.Playing)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldApplyPlaybackState_ShouldAllowIdleAfterEnded()
    {
        NativeVideoPlaybackEnd.ShouldApplyPlaybackState(PlaybackState.Ended, PlaybackState.Idle)
            .Should().BeTrue();
    }
}
