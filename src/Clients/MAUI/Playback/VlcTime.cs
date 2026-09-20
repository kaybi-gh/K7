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
    /// After a Direct Play reopen/seek with <c>:start-time</c>, demux can still report 0
    /// briefly. Freeze soft-sub cues at the pin until <see cref="MapDemuxSeconds"/> catches up.
    /// </summary>
    public static double FollowAfterReopen(
        double reportedSeconds,
        double pinnedSeconds,
        ref bool hold)
    {
        if (!hold)
            return reportedSeconds;

        if (reportedSeconds >= pinnedSeconds - 2.5 && reportedSeconds > 1)
        {
            hold = false;
            return reportedSeconds;
        }

        return Math.Max(0, pinnedSeconds);
    }

    /// <summary>
    /// LibVLC 4 after <c>:start-time</c> may report Time/Position over the remaining span
    /// (relative) or over the full media (absolute). Soft-sub cues must keep a sticky mapping
    /// for the whole open: treating relative Position as absolute
    /// (<c>Position * fullDuration</c>) makes cues run fast (rate = duration/remaining).
    /// Default after <c>:start-time</c> is relative; absolute Time near the epoch locks absolute.
    /// </summary>
    public static double MapDemuxSeconds(
        double timeSeconds,
        double position01,
        double durationSeconds,
        double epochSeconds,
        ref bool? demuxRelative)
    {
        var epoch = epochSeconds > 1 ? epochSeconds : 0;
        var duration = durationSeconds > 1 ? durationSeconds : 0;

        if (epoch > 1)
            demuxRelative ??= true;

        if (timeSeconds > 0)
        {
            if (epoch <= 1)
                return timeSeconds;

            // Absolute demux Time near the pin: lock absolute (overrides default relative).
            if (timeSeconds >= epoch - 2.5)
            {
                demuxRelative = false;
                return timeSeconds;
            }

            return demuxRelative is not false ? epoch + timeSeconds : timeSeconds;
        }

        if (duration <= 1 || position01 <= 0)
            return 0;

        if (epoch <= 1)
            return position01 * duration;

        if (demuxRelative is not false)
        {
            var remaining = Math.Max(1, duration - epoch);
            return epoch + position01 * remaining;
        }

        return position01 * duration;
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
