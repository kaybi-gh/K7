using System.Collections.Concurrent;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Extracts a sidecar WebVTT once per file/track. HLS segment requests must not wait
/// on ffmpeg: a blocked .vtt stalls the player's A/V prefetch and causes freeze/desync.
/// </summary>
internal static class HlsSubtitleVttExtractor
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ExtractionLocks = new();

    public static string GetCachePath(string transcodingPath, Guid indexedFileId, int trackIndex) =>
        Path.Combine(
            transcodingPath,
            indexedFileId.ToString("N"),
            Hls.SubtitlesCacheDirectoryName,
            $"{trackIndex}.vtt");

    public static bool IsReady(string vttCachePath) =>
        File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length > 0;

    public static async Task<bool> EnsureExtractedAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        string vttCachePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (IsReady(vttCachePath))
            return true;

        await ExtractAsync(mediaTranscoder, inputPath, trackIndex, vttCachePath, logger, cancellationToken);
        return IsReady(vttCachePath);
    }

    public static void StartBackgroundExtract(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        string vttCachePath,
        ILogger logger)
    {
        if (IsReady(vttCachePath))
            return;

        _ = ExtractAsync(mediaTranscoder, inputPath, trackIndex, vttCachePath, logger, CancellationToken.None);
    }

    private static async Task ExtractAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        string vttCachePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var semaphore = ExtractionLocks.GetOrAdd(vttCachePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsReady(vttCachePath))
                return;

            var directory = Path.GetDirectoryName(vttCachePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(vttCachePath))
                File.Delete(vttCachePath);

            await mediaTranscoder.ExtractSubtitleAsVttAsync(
                inputPath,
                trackIndex,
                vttCachePath,
                cancellationToken);

            if (File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length == 0)
            {
                logger.LogWarning(
                    "FFmpeg produced empty VTT for track {Track} from {Input} - removing cached file",
                    trackIndex,
                    inputPath);
                File.Delete(vttCachePath);
            }
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                ex,
                "Subtitle extraction failed for track {Track} from {Input} - format may not be convertible to WebVTT",
                trackIndex,
                inputPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Subtitle extraction failed for track {Track} from {Input}",
                trackIndex,
                inputPath);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
