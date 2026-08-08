namespace K7.Server.Domain.Constants;

/// <summary>
/// Heuristic for a listen that was abandoned almost immediately (track change / skip).
/// Shared by history status and <c>SkipCount</c> increments.
/// </summary>
public static class PlaybackSkipRules
{
    public const double MaxWatchedSeconds = 30;

    public static bool IsSkippedListen(bool isCompleted, bool isFinished, double watchedSeconds) =>
        !isCompleted && isFinished && watchedSeconds < MaxWatchedSeconds;

    public static double EffectiveWatchedSeconds(double watchedDurationSeconds, double positionSeconds) =>
        watchedDurationSeconds > 0 ? watchedDurationSeconds : Math.Max(0, positionSeconds);
}
