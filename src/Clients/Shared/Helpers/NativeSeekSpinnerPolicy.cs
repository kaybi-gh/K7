using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Seek buffering shows a spinner over the last frame. Do not arm the startup black veil
/// or <c>_awaitingFirstFrame</c> or a keep-content Playing event hides the spinner immediately.
/// Exo / LibVLC own the first-frame signal. Video.js Playing must not lift a Direct veil.
/// </summary>
public static class NativeSeekSpinnerPolicy
{
    public const double SeekFirstFrameSlopSeconds = 2.5;
    public static readonly TimeSpan SeekFirstFrameHoldWindow = TimeSpan.FromSeconds(15);

    public static bool ShouldArmStartupVeil(bool dimBackground) => dimBackground;

    /// <summary>
    /// Video.js has no decoder first-frame callback. Exo and LibVLC do: ignore Playing
    /// or a keep-content / leftover HLS frame lifts the veil too early.
    /// </summary>
    public static bool ShouldLiftVeilOnPlaying(
        bool awaitingFirstFrame,
        bool seekSpinnerActive,
        bool decoderOwnsFirstFrame)
    {
        if (!awaitingFirstFrame || seekSpinnerActive || decoderOwnsFirstFrame)
            return false;

        return true;
    }

    /// <summary>
    /// Original HLS is remux video + often AAC audio. After a seek the last video frame
    /// stays on screen while ffmpeg writes the landing window. Do not drop the spinner
    /// on a keep-content Playing / first-frame at the old position.
    /// </summary>
    public static bool ShouldHoldUntilDecoderReachesSeek(bool isOriginalQuality, bool isHls) =>
        isOriginalQuality && isHls;

    /// <summary>
    /// Hold the startup veil only for an in-flight remux seek. A leftover target from
    /// the previous title would ignore the new first frame and leave the spinner up.
    /// </summary>
    public static bool ShouldHoldStartupFirstFrameForSeek(
        bool isOriginalQuality,
        bool isHls,
        bool hasSeekTarget,
        TimeSpan sinceSeek)
    {
        if (!hasSeekTarget || sinceSeek < TimeSpan.Zero || sinceSeek > SeekFirstFrameHoldWindow)
            return false;

        return ShouldHoldUntilDecoderReachesSeek(isOriginalQuality, isHls);
    }

    public static bool ShouldHideOnPlaying(
        bool isWindows,
        bool sawSeekBuffering,
        bool holdUntilDecoderReachesSeek = false) =>
        isWindows && sawSeekBuffering && !holdUntilDecoderReachesSeek;

    public static bool ShouldShowOnMidPlayBuffering(bool isWindows, bool awaitingFirstFrame) =>
        isWindows && !awaitingFirstFrame;

    public static bool ShouldHideAfterWindowsInstantSeek(PlaybackState stateAfterDelay) =>
        stateAfterDelay == PlaybackState.Playing;

    public static bool ShouldAcceptSeekFirstFrame(double decoderPositionSeconds, double seekTargetSeconds)
    {
        if (seekTargetSeconds < 0)
            return true;

        if (decoderPositionSeconds <= 0)
            return false;

        return Math.Abs(decoderPositionSeconds - seekTargetSeconds) <= SeekFirstFrameSlopSeconds;
    }
}
