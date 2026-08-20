namespace K7.Server.Application.Helpers;

/// <summary>
/// When to restart ffmpeg for a missing playlist segment.
/// Never kill a running window to fill a hole: that freezes video while demuxed
/// audio copy keeps playing.
/// </summary>
internal static class HlsSegmentGenerationPolicy
{
    public static bool ShouldRestartForHole(
        int requestedIndex,
        bool ffmpegRunning,
        bool missingWithLaterSegments)
    {
        if (requestedIndex < 0 || ffmpegRunning)
            return false;

        return missingWithLaterSegments;
    }
}
