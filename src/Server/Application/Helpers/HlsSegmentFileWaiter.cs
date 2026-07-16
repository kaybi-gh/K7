using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Waits for HLS segment files produced by on-demand ffmpeg transcoding.
/// Re-kicks generation when ffmpeg exits without producing the segment, and
/// keeps waiting while a job is actively generating.
/// </summary>
internal static class HlsSegmentFileWaiter
{
    public static async Task<Exception?> WaitUntilAvailableAsync(
        string segmentPath,
        TranscodeJob job,
        Func<CancellationToken, Task> ensureGenerationAsync,
        CancellationToken cancellationToken,
        int maxTotalSeconds = 180,
        int pollingIntervalMs = 200)
    {
        var absoluteDeadline = DateTime.UtcNow.AddSeconds(maxTotalSeconds);
        var lastKick = DateTime.MinValue;

        // Kick generation with a job-scoped token so a browser abort cannot cancel
        // the ensure/start path before ffmpeg is launched. The poll below still
        // uses the request token so disconnected clients stop waiting.
        await ensureGenerationAsync(CancellationToken.None);

        while (!File.Exists(segmentPath) && DateTime.UtcNow < absoluteDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (job.FfmpegTask is { IsFaulted: true } failedTask)
                return failedTask.Exception?.GetBaseException()
                    ?? new InvalidOperationException("FFmpeg exited without generating the requested segment.");

            var ffmpegRunning = job.FfmpegTask is { IsCompleted: false };
            if (!ffmpegRunning && (DateTime.UtcNow - lastKick).TotalSeconds >= 1.5)
            {
                lastKick = DateTime.UtcNow;
                await ensureGenerationAsync(CancellationToken.None);
            }

            await Task.Delay(pollingIntervalMs, cancellationToken);
        }

        return File.Exists(segmentPath)
            ? null
            : new TimeoutException("FFmpeg did not generate the requested segment before the timeout.");
    }

    public static async Task WaitUntilReadableAsync(
        string segmentPath,
        CancellationToken cancellationToken,
        int timeoutSeconds = 5)
    {
        var accessDeadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < accessDeadline)
        {
            try
            {
                using var probe = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
