namespace K7.Server.Application.Helpers;

/// <summary>
/// When to restart ffmpeg for a missing playlist segment.
/// Never kill a running window to fill a hole: that freezes video while demuxed
/// audio copy keeps playing.
/// Never treat media segment 0 as a hole when later segments exist: HLS clients
/// often fetch playlist-start while playback is mid-file. A real seek to 0 uses
/// the backward-seek path instead.
/// </summary>
internal static class HlsSegmentGenerationPolicy
{
    public static bool ShouldRestartForHole(
        int requestedIndex,
        bool ffmpegRunning,
        bool missingWithLaterSegments)
    {
        if (requestedIndex <= 0 || ffmpegRunning)
            return false;

        return missingWithLaterSegments;
    }
}
