namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Where to start ffmpeg when <c>init.m4s</c> is missing. An empty cache (wipe / new job)
/// must not use a stale EOF <c>TargetSegmentIndex</c> - that remuxes from the end and the
/// client waits tens of seconds for init.
/// </summary>
internal static class TranscodeInitStartPolicy
{
    public static int ResolveStartIndex(
        int currentIndex,
        int lastClientMediaSegmentRequest,
        int segmentCount)
    {
        if (segmentCount <= 0)
            return 0;

        if (currentIndex >= 0)
            return Math.Clamp(currentIndex - 5, 0, segmentCount - 1);

        if (lastClientMediaSegmentRequest >= 0)
            return Math.Clamp(lastClientMediaSegmentRequest, 0, segmentCount - 1);

        return 0;
    }
}
