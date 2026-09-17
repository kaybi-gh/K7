namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// When an empty shared HLS output should reset in-memory job state.
/// Remux copy advertises Target = EOF and writes to head-* staging first, so a
/// high Target plus empty shared output is a cold start, not a wipe.
/// Encode resume starts ffmpeg at N with Target = N + Buffer. Shared output is
/// empty until the first .m4s lands. That is also a cold window, not a wipe.
/// </summary>
internal static class TranscodeWipedOutputPolicy
{
    public static bool NeedsReset(
        bool outputDirectoryExists,
        bool isFfmpegRunning,
        bool isCopyRemux,
        bool hasRemuxStaging,
        bool hasObservedReadyOutput,
        int lastRequestedSegmentIndex,
        int targetSegmentIndex,
        int windowStartIndex,
        int generatingFromSegmentIndex,
        int bufferSize)
    {
        if (!outputDirectoryExists)
        {
            return isFfmpegRunning
                || lastRequestedSegmentIndex >= 0
                || targetSegmentIndex > 0;
        }

        // Live remux to EOF with empty shared output is the normal cold start.
        if (isCopyRemux && isFfmpegRunning && hasRemuxStaging)
            return false;

        // Files existed then vanished under this generation.
        if (hasObservedReadyOutput)
            return true;

        if (isFfmpegRunning)
        {
            // Encode window (0 or resume N) has not landed yet.
            if (!isCopyRemux)
                return false;

            return !hasRemuxStaging;
        }

        return lastRequestedSegmentIndex >= 0
            || windowStartIndex > 0
            || generatingFromSegmentIndex > bufferSize
            || targetSegmentIndex > bufferSize;
    }
}
