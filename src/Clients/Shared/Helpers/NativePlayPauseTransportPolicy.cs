using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Overlay play/pause must not follow remux 503 Idle/Paused flaps. The glyph follows
/// the user toggle until Ended. Stale engine pauses during startup are ignored.
/// </summary>
public static class NativePlayPauseTransportPolicy
{
    public static readonly TimeSpan StalePauseGrace = TimeSpan.FromSeconds(2);

    public static bool ShouldShowPauseGlyph(bool userPaused, PlaybackState state) =>
        !userPaused && state is not PlaybackState.Ended;

    public static bool ShouldRequestPauseOnToggle(bool userPaused, PlaybackState state) =>
        ShouldShowPauseGlyph(userPaused, state);

    public static bool ShouldIgnoreEngineIdleOrPaused(
        bool userPaused,
        bool awaitingFirstFrame,
        DateTime firstFrameUtc,
        DateTime utcNow,
        PlaybackState state)
    {
        if (userPaused)
            return false;

        if (state is not (PlaybackState.Idle or PlaybackState.Paused))
            return false;

        return awaitingFirstFrame || utcNow - firstFrameUtc < StalePauseGrace;
    }
}
