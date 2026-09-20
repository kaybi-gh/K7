using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Natural end-of-file detection. <c>MediaEnded</c> is not always raised for HLS.
/// Stopped/Paused near duration, a frozen last frame (Exo READY + playWhenReady without
/// isPlaying), and an explicit seek to EOF must complete the episode so the next-episode
/// offer can run. A <c>Stop()</c> during a source swap must not be treated as ended.
/// </summary>
public static class NativeVideoPlaybackEnd
{
    public const double MinDurationSeconds = 5;
    public const double EndToleranceSeconds = 1.25;
    public const double SeekToEndToleranceSeconds = 2.0;

    public static bool IsAtMediaEnd(
        double positionSeconds,
        double durationSeconds,
        double toleranceSeconds = EndToleranceSeconds)
    {
        if (durationSeconds <= MinDurationSeconds)
            return false;

        return positionSeconds >= durationSeconds - toleranceSeconds;
    }

    public static bool IsSeekToMediaEnd(double targetSeconds, double durationSeconds) =>
        IsAtMediaEnd(targetSeconds, durationSeconds, SeekToEndToleranceSeconds);

    public static bool ShouldTreatStoppedAsEnded(
        bool isOpeningSource,
        bool isVisible,
        PlaybackState currentState,
        double durationSeconds,
        double positionSeconds)
    {
        if (isOpeningSource || !isVisible)
            return false;

        if (currentState == PlaybackState.Ended)
            return true;

        return IsAtMediaEnd(positionSeconds, durationSeconds);
    }

    /// <summary>
    /// Promote a stalled clock at EOF to Ended. Keep Buffering (last-segment fetch) and a
    /// still-moving playhead so the last frames can finish naturally.
    /// </summary>
    public static PlaybackState PromoteIfMediaEnded(
        PlaybackState mapped,
        bool engineIsPlaying,
        bool isOpeningSource,
        bool isVisible,
        double durationSeconds,
        double positionSeconds)
    {
        if (mapped == PlaybackState.Ended)
            return mapped;

        if (isOpeningSource || !isVisible || engineIsPlaying)
            return mapped;

        if (mapped is PlaybackState.Buffering)
            return mapped;

        if (!IsAtMediaEnd(positionSeconds, durationSeconds))
            return mapped;

        return PlaybackState.Ended;
    }

    /// <summary>
    /// After Ended, engine Pause/Buffering callbacks must not hide the next-episode offer.
    /// Idle (close) and Playing (replay / next title) still apply.
    /// </summary>
    public static bool ShouldApplyPlaybackState(PlaybackState current, PlaybackState next)
    {
        if (current == next)
            return false;

        if (current == PlaybackState.Ended
            && next is PlaybackState.Paused or PlaybackState.Buffering or PlaybackState.Unknown)
            return false;

        return true;
    }
}
