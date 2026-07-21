using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Waits for HLS segment files produced by on-demand ffmpeg transcoding.
/// Re-kicks generation when ffmpeg exits without producing the segment, and
/// keeps waiting while a job is actively generating.
/// </summary>
internal static class HlsSegmentFileWaiter
{
    public const string InitSegmentFileName = "init.m4s";

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

        while (!HasNonEmptyContent(segmentPath) && DateTime.UtcNow < absoluteDeadline)
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

        return HasNonEmptyContent(segmentPath)
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
            cancellationToken.ThrowIfCancellationRequested();

            if (!HasNonEmptyContent(segmentPath))
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            try
            {
                using var probe = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (probe.Length > 0)
                    return;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public static bool HasNonEmptyContent(string segmentPath)
    {
        if (!File.Exists(segmentPath))
            return false;

        try
        {
            return new FileInfo(segmentPath).Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Segment 0 also requires a non-empty init.m4s; later segments only need their .m4s file.
    /// </summary>
    public static bool IsSegmentReadyOnDisk(string outputDirectory, int segmentIndex)
    {
        var segmentPath = Path.Combine(outputDirectory, $"{segmentIndex}.m4s");
        if (!HasNonEmptyContent(segmentPath))
            return false;

        if (segmentIndex != 0)
            return true;

        return HasNonEmptyContent(GetInitSegmentPath(outputDirectory));
    }

    public static string GetInitSegmentPath(string outputDirectory) =>
        Path.Combine(outputDirectory, InitSegmentFileName);
}
