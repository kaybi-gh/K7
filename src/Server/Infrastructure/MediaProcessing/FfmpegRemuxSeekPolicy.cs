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
        IEnumerable<(int TipIndex, int UntilInclusive, bool Running)> liveHeads,
        IReadOnlyList<Domain.Entities.HlsSegment> allSegments)
    {
        var min = double.PositiveInfinity;
        foreach (var (tip, until, running) in liveHeads)
        {
            if (!running)
                continue;

            if (requestedIndex >= tip && requestedIndex <= until)
                return 0;

            var from = Math.Min(tip, requestedIndex);
            var to = Math.Max(tip, requestedIndex);
            min = Math.Min(min, SumDurationsSeconds(allSegments, from, to));
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
