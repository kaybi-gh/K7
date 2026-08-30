#if ANDROID || WINDOWS
namespace K7.Clients.MAUI.Playback;

/// <summary>
/// LibVLC 4 documents <c>Time</c> / <c>Length</c> / <c>SetTime</c> as microseconds.
/// Some builds / paths still behave like milliseconds. Detect from Length vs known duration.
/// </summary>
internal static class VlcTime
{
    public const long MicrosecondsPerSecond = 1_000_000;
    public const long MillisecondsPerSecond = 1_000;

    public static double ToSeconds(long ticks, long ticksPerSecond) =>
        ticks > 0 && ticksPerSecond > 0 ? ticks / (double)ticksPerSecond : 0;

    public static double ToSeconds(long ticks) =>
        ToSeconds(ticks, MicrosecondsPerSecond);

    public static long FromSeconds(double seconds, long ticksPerSecond) =>
        seconds > 0 && ticksPerSecond > 0 ? (long)(seconds * ticksPerSecond) : 0;

    public static long FromSeconds(double seconds) =>
        FromSeconds(seconds, MicrosecondsPerSecond);

    /// <summary>
    /// Early demux can report tiny Length (e.g. 177). Those must not lock the time scale.
    /// </summary>
    public static bool IsReliableLength(long lengthTicks, double knownDurationSeconds)
    {
        if (lengthTicks <= 0)
            return false;

        var asUs = lengthTicks / (double)MicrosecondsPerSecond;
        var asMs = lengthTicks / (double)MillisecondsPerSecond;

        if (knownDurationSeconds > 1)
        {
            if (asUs >= knownDurationSeconds * 0.5 && asUs <= knownDurationSeconds * 2)
                return true;
            if (asMs >= knownDurationSeconds * 0.5 && asMs <= knownDurationSeconds * 2)
                return true;
            return false;
        }

        return asUs >= 5 || asMs >= 5;
    }

    /// <summary>
    /// Pick us vs ms so Length matches the known file duration (metadata).
    /// Defaults to LibVLC 4 microseconds when Length is junk or ambiguous.
    /// </summary>
    public static long DetectTicksPerSecond(long lengthTicks, double knownDurationSeconds)
    {
        if (lengthTicks <= 0)
            return MicrosecondsPerSecond;

        var asUs = lengthTicks / (double)MicrosecondsPerSecond;
        var asMs = lengthTicks / (double)MillisecondsPerSecond;

        // Junk early Length (e.g. 177): keep LibVLC 4 default.
        if (asUs < 2 && asMs < 2)
            return MicrosecondsPerSecond;

        if (knownDurationSeconds > 1)
        {
            var errUs = Math.Abs(asUs - knownDurationSeconds) / knownDurationSeconds;
            var errMs = Math.Abs(asMs - knownDurationSeconds) / knownDurationSeconds;
            if (errMs + 0.02 < errUs && errMs < 0.25)
                return MillisecondsPerSecond;
            if (errUs < 0.25)
                return MicrosecondsPerSecond;
            return MicrosecondsPerSecond;
        }

        // No metadata: ms only when ticks look like a real film in ms and nonsense in us.
        if (asMs >= 30 && asUs < 30)
            return MillisecondsPerSecond;

        return MicrosecondsPerSecond;
    }

    /// <summary>
    /// After a Direct Play reopen/seek, VLC often reports <c>Time</c> from 0 while
    /// <c>:start-time</c> already placed the picture. Prefer demux-relative Time
    /// (pin + Time) so sidecar cues stay locked to audio; wall-clock is only a
    /// fallback while Time is still 0.
    /// </summary>
    public static double FollowAfterReopen(
        double reportedSeconds,
        double pinnedSeconds,
        DateTime holdStartedUtc,
        double rate,
        bool firstFrameSeen,
        ref bool hold)
    {
        if (!hold)
            return reportedSeconds;

        if (!firstFrameSeen)
            return Math.Max(0, pinnedSeconds);

        // Absolute media timeline caught up near the seek pin.
        if (reportedSeconds >= pinnedSeconds - 2.5)
        {
            hold = false;
            return reportedSeconds;
        }

        // Relative timeline after :start-time: Time restarts near 0 and tracks demux/audio.
        if (reportedSeconds > 0.05)
            return pinnedSeconds + reportedSeconds;

        // Time still 0: brief wall-clock until the demux ticks.
        var speed = rate > 0 ? rate : 1;
        return pinnedSeconds + Math.Max(0, (DateTime.UtcNow - holdStartedUtc).TotalSeconds) * speed;
    }

    public static bool TryAcceptDuration(double reportedSeconds, double knownSeconds, out double seconds)
    {
        seconds = reportedSeconds;
        if (reportedSeconds <= 0)
            return false;

        if (knownSeconds > 1
            && (reportedSeconds < knownSeconds * 0.5 || reportedSeconds > knownSeconds * 2))
        {
            return false;
        }

        return true;
    }
}
#endif
