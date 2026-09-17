namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Multi-head remux: keep live heads for linear catch-up, spawn a new head when the
/// client lands far from any live tip. Never purge ready shared segments.
/// </summary>
internal static class FfmpegRemuxSeekPolicy
{
    /// <summary>
    /// Spawn a new remux head when the nearest live tip is farther than this (~60s).
    /// </summary>
    public const double SpawnHeadDistanceSeconds = 60;

    /// <summary>
    /// Forward prefetch beyond this many segments is treated as a seek jump for logging.
    /// </summary>
    public const int ForwardSeekThresholdSegments = 10;

    public static bool IsClientSeekJump(
        int previousClientRequest,
        int requestedIndex,
        int forwardThreshold = ForwardSeekThresholdSegments)
    {
        if (requestedIndex < 0)
            return false;

        if (previousClientRequest < 0)
            return requestedIndex > 0;

        if (requestedIndex < previousClientRequest)
            return true;

        return requestedIndex - previousClientRequest > forwardThreshold;
    }

    /// <summary>
    /// True when remux should start a new ffmpeg head at <paramref name="requestedIndex"/>.
    /// Ready segments are immutable: never spawn to "fix" them.
    /// </summary>
    public static bool ShouldSpawnRemuxHead(
        bool remuxCopy,
        bool segmentReady,
        bool coveredByLiveHead,
        double minDistanceSecondsToLiveHead)
    {
        if (!remuxCopy || segmentReady)
            return false;

        // Covered by a live head: wait for promote.
        if (coveredByLiveHead)
            return false;

        // Near an existing tip: let that head catch up (~60s).
        if (minDistanceSecondsToLiveHead <= SpawnHeadDistanceSeconds)
            return false;

        return true;
    }

    public static double MinDistanceSecondsToLiveHead(
        int requestedIndex,
        IEnumerable<(int FromIndex, int TipIndex, int UntilInclusive, bool Running)> liveHeads,
        IReadOnlyList<Domain.Entities.HlsSegment> allSegments)
    {
        var min = double.PositiveInfinity;
        foreach (var (from, tip, _, running) in liveHeads)
        {
            if (!running)
                continue;

            // A head never writes anything before its own start. A request behind From
            // (restart from the beginning while a resume head runs at 113, seek back) can
            // only be served by a new head: treating it as "distance 0" made the client
            // wait forever on a segment no process would ever produce.
            if (requestedIndex < from)
                continue;

            // A head only actually covers up to its live TIP. Its target end (UntilInclusive)
            // is EOF for a copy head, so [tip, until] would wrongly mark every forward seek as
            // covered and make the client wait on linear catch-up instead of spawning a head.
            if (requestedIndex <= tip)
                return 0;

            min = Math.Min(min, SumDurationsSeconds(allSegments, tip, requestedIndex));
        }

        return double.IsPositiveInfinity(min) ? double.PositiveInfinity : min;
    }

    private static double SumDurationsSeconds(
        IReadOnlyList<Domain.Entities.HlsSegment> allSegments,
        int fromInclusive,
        int toExclusiveEnd)
    {
        if (allSegments.Count == 0 || fromInclusive >= toExclusiveEnd)
            return 0;

        fromInclusive = Math.Clamp(fromInclusive, 0, allSegments.Count - 1);
        toExclusiveEnd = Math.Clamp(toExclusiveEnd, 0, allSegments.Count);
        long ms = 0;
        for (var i = fromInclusive; i < toExclusiveEnd; i++)
            ms += allSegments[i].Duration;

        return ms / 1000.0;
    }
}
