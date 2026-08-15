using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Natural end-of-file detection for CommunityToolkit MediaElement on Android/iOS.
/// <c>MediaEnded</c> is not always raised for HLS; <c>Stopped</c> near duration is the fallback.
/// A <c>Stop()</c> during a source swap must not be treated as ended.
/// </summary>
public static class NativeVideoPlaybackEnd
{
    public const double MinDurationSeconds = 5;
    public const double EndToleranceSeconds = 1.25;

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

        return durationSeconds > MinDurationSeconds
            && positionSeconds >= durationSeconds - EndToleranceSeconds;
    }
}
