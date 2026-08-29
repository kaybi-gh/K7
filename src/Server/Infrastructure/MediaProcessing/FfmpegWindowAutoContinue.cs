namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Continues an idle ffmpeg job toward the client Target, then stretches that Target
/// to a full BufferSize window from the frontier. Catch-up 1-segment windows restart
/// ffmpeg every GET and cut remux video on ExoPlayer.
/// </summary>
internal static class FfmpegWindowAutoContinue
{
    /// <summary>
    /// True when ready segments lag the sliding-window Target and more playlist entries remain.
    /// </summary>
    public static bool ShouldContinueTowardClientTarget(
        int currentSegmentIndex,
        int targetSegmentIndex,
        int segmentCount)
    {
        if (currentSegmentIndex < 0 || segmentCount <= 0)
            return false;

        if (currentSegmentIndex + 1 >= segmentCount)
            return false;

        return targetSegmentIndex > currentSegmentIndex;
    }

    /// <summary>
    /// Ready segments caught the request Target, but the client is still watching near
    /// the frontier. Raise Target so the next BufferSize window starts before underrun.
    /// </summary>
    public static bool ShouldKeepLookahead(
        int currentSegmentIndex,
        int targetSegmentIndex,
        int lastRequestedSegmentIndex,
        int bufferSize,
        int segmentCount)
    {
        if (currentSegmentIndex < 0 || segmentCount <= 0)
            return false;

        if (currentSegmentIndex + 1 >= segmentCount)
            return false;

        if (targetSegmentIndex > currentSegmentIndex)
            return false;

        if (lastRequestedSegmentIndex < 0)
            return false;

        return currentSegmentIndex - lastRequestedSegmentIndex <= Math.Max(bufferSize, 1);
    }

    /// <summary>
    /// Inclusive Target for a new request. Remux copy runs to EOF (seek starts a new
    /// process). Encode stays on the configured BufferSize window.
    /// </summary>
    public static int ResolveAdvertisedTarget(
        int requestedSegmentIndex,
        int currentTargetSegmentIndex,
        int bufferSize,
        int segmentCount,
        bool remuxToEnd)
    {
        if (segmentCount <= 0)
            return currentTargetSegmentIndex;

        var lastIndex = segmentCount - 1;
        if (remuxToEnd)
            return lastIndex;

        var floor = requestedSegmentIndex + Math.Max(bufferSize, 1);
        return Math.Min(Math.Max(currentTargetSegmentIndex, floor), lastIndex);
    }

    /// <summary>
    /// Inclusive Target after a sequential continue. At least start + BufferSize so the
    /// next ffmpeg window is a real lookahead, not one leftover index from an old request.
    /// </summary>
    public static int ResolveContinueTarget(
        int startSegmentIndex,
        int currentTargetSegmentIndex,
        int bufferSize,
        int segmentCount)
    {
        if (segmentCount <= 0)
            return currentTargetSegmentIndex;

        var lastIndex = segmentCount - 1;
        if (startSegmentIndex < 0)
            return Math.Clamp(currentTargetSegmentIndex, 0, lastIndex);

        var floor = startSegmentIndex + Math.Max(bufferSize, 1);
        return Math.Min(Math.Max(currentTargetSegmentIndex, floor), lastIndex);
    }
}
