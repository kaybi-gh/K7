using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Maps Media3 IPlayer playbackState to K7 PlaybackState when Exo is not hosted by
/// CommunityToolkit MediaManager (Android TV tuned player).
/// </summary>
public static class ExoPlaybackStateMapping
{
    public const int StateIdle = 1;
    public const int StateBuffering = 2;
    public const int StateReady = 3;
    public const int StateEnded = 4;

    public static PlaybackState Map(int exoState, bool playWhenReady, bool isPlaying)
    {
        if (exoState == StateEnded)
            return PlaybackState.Ended;
        if (exoState == StateBuffering)
            return PlaybackState.Buffering;
        if (exoState == StateReady)
        {
            return isPlaying || playWhenReady
                ? PlaybackState.Playing
                : PlaybackState.Paused;
        }

        return playWhenReady
            ? PlaybackState.Buffering
            : PlaybackState.Idle;
    }
}
