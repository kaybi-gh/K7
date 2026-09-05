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
}
