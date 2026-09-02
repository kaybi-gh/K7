using FluentAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ExoPlaybackStateMappingTests
{
    [Test]
    public void Map_ShouldBeEnded_WhenExoEnded()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateEnded, playWhenReady: false, isPlaying: false)
            .Should().Be(PlaybackState.Ended);
    }

    [Test]
    public void Map_ShouldBeBuffering_WhenExoBuffering()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateBuffering, playWhenReady: true, isPlaying: false)
            .Should().Be(PlaybackState.Buffering);
    }

    [Test]
    public void Map_ShouldBePlaying_WhenReadyAndPlaying()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateReady, playWhenReady: true, isPlaying: true)
            .Should().Be(PlaybackState.Playing);
    }

    [Test]
    public void Map_ShouldBePlaying_WhenReadyAndPlayWhenReady()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateReady, playWhenReady: true, isPlaying: false)
            .Should().Be(PlaybackState.Playing);
    }

    [Test]
    public void Map_ShouldBePaused_WhenReadyAndNotPlaying()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateReady, playWhenReady: false, isPlaying: false)
            .Should().Be(PlaybackState.Paused);
    }

    [Test]
    public void Map_ShouldBeBuffering_WhenIdleAndPlayWhenReady()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateIdle, playWhenReady: true, isPlaying: false)
            .Should().Be(PlaybackState.Buffering);
    }

    [Test]
    public void Map_ShouldBeIdle_WhenIdleAndNotPlayWhenReady()
    {
        ExoPlaybackStateMapping
            .Map(ExoPlaybackStateMapping.StateIdle, playWhenReady: false, isPlaying: false)
            .Should().Be(PlaybackState.Idle);
    }
}
