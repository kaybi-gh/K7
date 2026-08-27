namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Continues an idle ffmpeg job only toward the client-advertised Target (requested + buffer),
/// so window boundaries do not underrun ExoPlayer without remuxing the whole file ahead of demand.
/// </summary>
internal static class FfmpegWindowAutoContinue
{
    /// <summary>
    /// True when ready segments lag the sliding-window Target and more playlist entries remain.
    /// Does not raise Target - that stays driven by segment HTTP requests.
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
}
