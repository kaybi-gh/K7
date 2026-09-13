using AwesomeAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativePlayPauseTransportPolicyTests
{
    [Test]
    public void ShouldShowPauseGlyph_ShouldBeTrue_UntilUserPauses()
    {
        NativePlayPauseTransportPolicy.ShouldShowPauseGlyph(userPaused: false, PlaybackState.Idle)
            .Should().BeTrue();
        NativePlayPauseTransportPolicy.ShouldShowPauseGlyph(userPaused: false, PlaybackState.Playing)
            .Should().BeTrue();
        NativePlayPauseTransportPolicy.ShouldShowPauseGlyph(userPaused: true, PlaybackState.Playing)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldShowPauseGlyph_ShouldBeFalse_WhenEnded()
    {
        NativePlayPauseTransportPolicy.ShouldShowPauseGlyph(userPaused: false, PlaybackState.Ended)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldRequestPauseOnToggle_ShouldMatchPauseGlyph()
    {
        NativePlayPauseTransportPolicy.ShouldRequestPauseOnToggle(userPaused: false, PlaybackState.Buffering)
            .Should().BeTrue();
        NativePlayPauseTransportPolicy.ShouldRequestPauseOnToggle(userPaused: true, PlaybackState.Paused)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldIgnoreEngineIdleOrPaused_ShouldBeTrue_DuringStartup()
    {
        NativePlayPauseTransportPolicy.ShouldIgnoreEngineIdleOrPaused(
                userPaused: false,
                awaitingFirstFrame: true,
                firstFrameUtc: DateTime.MinValue,
                utcNow: DateTime.UtcNow,
                state: PlaybackState.Paused)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldIgnoreEngineIdleOrPaused_ShouldBeTrue_DuringGraceAfterFirstFrame()
    {
        var first = DateTime.UtcNow;
        NativePlayPauseTransportPolicy.ShouldIgnoreEngineIdleOrPaused(
                userPaused: false,
                awaitingFirstFrame: false,
                firstFrameUtc: first,
                utcNow: first.AddSeconds(1),
                state: PlaybackState.Idle)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldIgnoreEngineIdleOrPaused_ShouldBeFalse_AfterGrace()
    {
        var first = DateTime.UtcNow;
        NativePlayPauseTransportPolicy.ShouldIgnoreEngineIdleOrPaused(
                userPaused: false,
                awaitingFirstFrame: false,
                firstFrameUtc: first,
                utcNow: first.AddSeconds(3),
                state: PlaybackState.Paused)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldIgnoreEngineIdleOrPaused_ShouldBeFalse_WhenUserPaused()
    {
        NativePlayPauseTransportPolicy.ShouldIgnoreEngineIdleOrPaused(
                userPaused: true,
                awaitingFirstFrame: true,
                firstFrameUtc: DateTime.UtcNow,
                utcNow: DateTime.UtcNow,
                state: PlaybackState.Paused)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldIgnoreEngineIdleOrPaused_ShouldBeFalse_WhenPlaying()
    {
        NativePlayPauseTransportPolicy.ShouldIgnoreEngineIdleOrPaused(
                userPaused: false,
                awaitingFirstFrame: true,
                firstFrameUtc: DateTime.UtcNow,
                utcNow: DateTime.UtcNow,
                state: PlaybackState.Playing)
            .Should().BeFalse();
    }
}
