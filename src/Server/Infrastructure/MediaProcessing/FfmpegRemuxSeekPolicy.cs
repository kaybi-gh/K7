namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Remux copy runs one ffmpeg to EOF. Do not kill that process when the client
/// seeks to a segment it already wrote or is still writing.
/// </summary>
internal static class FfmpegRemuxSeekPolicy
{
    public static bool ShouldKeepRunningProcess(
        bool remuxToEnd,
        bool segmentReady,
        bool ffmpegRunning,
        int requestedIndex,
        int generatingFrom,
        int generatingUntil)
    {
        if (!remuxToEnd)
            return false;

        if (segmentReady)
            return true;

        if (!ffmpegRunning || generatingFrom < 0 || generatingUntil < 0)
            return false;

        return requestedIndex >= generatingFrom && requestedIndex <= generatingUntil;
    }
}
