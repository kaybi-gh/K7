using System.Collections.Concurrent;
using System.Diagnostics;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Extracts a sidecar WebVTT once per file/track. HLS segment requests must not wait
/// on ffmpeg: a blocked .vtt stalls the player's A/V prefetch and causes freeze/desync.
/// </summary>
/// <remarks>
/// A full extraction reads the media file from start to end, which takes minutes on a large
/// file or slow storage. Two things keep subtitles usable meanwhile:
/// windows (a short time range around the playback position, reached by an input seek) and
/// the persistent cache filled at scan time when the library enables subtitle pre-extraction.
/// </remarks>
internal static class HlsSubtitleVttExtractor
{
    /// <summary>
    /// Length of an extraction window. A multiple of the HLS subtitle segment duration, so a
    /// segment never spans two windows.
    /// </summary>
    public const int WindowSeconds = 120;

    /// <summary>
    /// A window starts this much earlier than its range: subtitle packets are stored at their
    /// start time, so a cue still on screen at the window start is located before it.
    /// </summary>
    private const int WindowLookBehindSeconds = 30;

    /// <summary>The window holding the position this far ahead is prepared too.</summary>
    private const int WindowLookAheadSeconds = 60;

    private const int MaxWindowIndex = 100_000;

    private static readonly TimeSpan SlowExtractionThreshold = TimeSpan.FromMinutes(1);

    private static readonly ConcurrentDictionary<string, Lazy<Task>> RunningExtractions = new();

    public static string GetCachePath(string transcodingPath, Guid indexedFileId, int trackIndex) =>
        Path.Combine(
            transcodingPath,
            indexedFileId.ToString("N"),
            Hls.SubtitlesCacheDirectoryName,
            $"{trackIndex}.vtt");

    /// <summary>
    /// Directory of the pre-extracted tracks of a file, next to its seekbar thumbnail sheet
    /// (<c>indexed-files/{id}/thumbnails.jpg</c>). The transcoding cache is purged with stale jobs.
    /// </summary>
    public static string GetPersistentCacheDirectory(string metadatasPath, Guid indexedFileId) =>
        Path.Combine(metadatasPath, "indexed-files", indexedFileId.ToString(), "subtitles");

    public static string GetPersistentCachePath(string metadatasPath, Guid indexedFileId, int trackIndex) =>
        Path.Combine(GetPersistentCacheDirectory(metadatasPath, indexedFileId), $"{trackIndex}.vtt");

    public static string GetWindowCachePath(string transcodingPath, Guid indexedFileId, int trackIndex, int windowIndex) =>
        Path.Combine(
            transcodingPath,
            indexedFileId.ToString("N"),
            Hls.SubtitlesCacheDirectoryName,
            $"{trackIndex}.w{windowIndex}.vtt");

    public static int GetWindowIndex(double positionSeconds) =>
        double.IsFinite(positionSeconds) && positionSeconds > 0
            ? (int)Math.Min(positionSeconds / WindowSeconds, MaxWindowIndex)
            : 0;

    public static bool IsReady(string vttCachePath) =>
        File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length > 0;

    /// <summary>
    /// Path of the complete VTT of a track, pre-extracted or extracted on demand.
    /// Null when the track still has to be extracted.
    /// </summary>
    public static string? FindReadyPath(PathsConfiguration paths, Guid indexedFileId, int trackIndex)
    {
        if (!string.IsNullOrEmpty(paths.Metadatas))
        {
            var persistentPath = GetPersistentCachePath(paths.Metadatas, indexedFileId, trackIndex);
            if (IsReady(persistentPath))
                return persistentPath;
        }

        var cachePath = GetCachePath(paths.Transcoding, indexedFileId, trackIndex);
        return IsReady(cachePath) ? cachePath : null;
    }

    /// <summary>
    /// Where an on-demand full extraction is written. A library with subtitle pre-extraction
    /// keeps it for good, which also covers the files indexed before the capability was enabled.
    /// </summary>
    public static async Task<string> ResolveExtractionPathAsync(
        IApplicationDbContext context,
        PathsConfiguration paths,
        IndexedFile indexedFile,
        int trackIndex,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(paths.Metadatas))
        {
            var preExtractionEnabled = await context.Libraries
                .AsNoTracking()
                .Where(l => l.Id == indexedFile.LibraryId)
                .Select(l => l.SubtitlePreExtractionEnabled)
                .FirstOrDefaultAsync(cancellationToken);

            if (preExtractionEnabled)
                return GetPersistentCachePath(paths.Metadatas, indexedFile.Id, trackIndex);
        }

        return GetCachePath(paths.Transcoding, indexedFile.Id, trackIndex);
    }

    /// <summary>
    /// Drops the pre-extracted tracks of a file: it was removed, or its content changed and
    /// the tracks no longer match.
    /// </summary>
    public static void DeletePersistentCache(PathsConfiguration paths, Guid indexedFileId, ILogger logger)
    {
        if (string.IsNullOrEmpty(paths.Metadatas))
            return;

        var directory = GetPersistentCacheDirectory(paths.Metadatas, indexedFileId);
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

            // Drop the file folder only when nothing else remains (the thumbnail sheet lives beside subtitles).
            var fileDirectory = Directory.GetParent(directory);
            if (fileDirectory is { Exists: true } && !fileDirectory.EnumerateFileSystemInfos().Any())
                fileDirectory.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete pre-extracted subtitles of file {IndexedFileId}", indexedFileId);
        }
    }

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

        await RunOnce(vttCachePath, () => ExtractAsync(mediaTranscoder, inputPath, trackIndex, vttCachePath, logger))
            .WaitAsync(cancellationToken);
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

        _ = RunOnce(vttCachePath, () => ExtractAsync(mediaTranscoder, inputPath, trackIndex, vttCachePath, logger));
    }

    /// <summary>
    /// Extracts several tracks in a single read of the media file, replacing what is cached.
    /// Unlike the on-demand paths, failures are thrown to the caller.
    /// </summary>
    public static async Task ExtractAllAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        IReadOnlyDictionary<int, string> vttCachePathsByTrackIndex,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (vttCachePathsByTrackIndex.Count == 0)
            return;

        var tempPathsByTrackIndex = vttCachePathsByTrackIndex
            .ToDictionary(x => x.Key, x => CreateTempPath(x.Value));
        try
        {
            foreach (var tempPath in tempPathsByTrackIndex.Values)
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

            await mediaTranscoder.ExtractSubtitlesAsVttAsync(inputPath, tempPathsByTrackIndex, cancellationToken);

            foreach (var (trackIndex, vttCachePath) in vttCachePathsByTrackIndex)
            {
                if (!TryPublish(tempPathsByTrackIndex[trackIndex], vttCachePath))
                {
                    logger.LogWarning(
                        "FFmpeg produced empty VTT for track {Track} from {Input} - nothing cached",
                        trackIndex,
                        inputPath);
                }
            }
        }
        finally
        {
            foreach (var tempPath in tempPathsByTrackIndex.Values)
                TryDelete(tempPath);
        }
    }

    /// <summary>
    /// Raw VTT of one window, or null while it is being extracted (started here if needed).
    /// It also holds the cues that begin shortly before the window. The next window is
    /// prepared too: a player reading segments in order then finds it ready and gets no 503.
    /// </summary>
    public static async Task<string?> ReadWindowAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        string transcodingPath,
        Guid indexedFileId,
        int trackIndex,
        int windowIndex,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string? content = null;
        for (var index = windowIndex; index <= Math.Min(windowIndex + 1, MaxWindowIndex); index++)
        {
            var windowCachePath = GetWindowCachePath(transcodingPath, indexedFileId, trackIndex, index);
            if (!File.Exists(windowCachePath))
                StartBackgroundWindowExtract(mediaTranscoder, inputPath, trackIndex, index, windowCachePath, logger);
            else if (index == windowIndex)
                content = await File.ReadAllTextAsync(windowCachePath, cancellationToken);
        }

        return content;
    }

    /// <summary>
    /// VTT made of the windows already extracted around <paramref name="positionSeconds"/>,
    /// flagged as partial. Null while the window holding the position is being extracted.
    /// Starts the windows that playback is about to need.
    /// </summary>
    public static async Task<string?> ReadPartialAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        string transcodingPath,
        Guid indexedFileId,
        int trackIndex,
        double positionSeconds,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string WindowPath(int windowIndex) =>
            GetWindowCachePath(transcodingPath, indexedFileId, trackIndex, windowIndex);

        var current = GetWindowIndex(positionSeconds);
        var ahead = GetWindowIndex(Math.Max(0, positionSeconds) + WindowLookAheadSeconds);
        for (var windowIndex = current; windowIndex <= ahead; windowIndex++)
        {
            if (!File.Exists(WindowPath(windowIndex)))
                StartBackgroundWindowExtract(mediaTranscoder, inputPath, trackIndex, windowIndex, WindowPath(windowIndex), logger);
        }

        if (!File.Exists(WindowPath(current)))
            return null;

        var first = current;
        while (first > 0 && File.Exists(WindowPath(first - 1)))
            first--;

        var last = current;
        while (last < MaxWindowIndex && File.Exists(WindowPath(last + 1)))
            last++;

        var windows = new List<string>(last - first + 1);
        for (var windowIndex = first; windowIndex <= last; windowIndex++)
            windows.Add(await File.ReadAllTextAsync(WindowPath(windowIndex), cancellationToken));

        return WebVttSegmenter.MergeWindows(windows, first * (double)WindowSeconds, WindowSeconds);
    }

    private static void StartBackgroundWindowExtract(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        int windowIndex,
        string windowCachePath,
        ILogger logger)
    {
        _ = RunOnce(
            windowCachePath,
            () => ExtractWindowAsync(mediaTranscoder, inputPath, trackIndex, windowIndex, windowCachePath, logger));
    }

    /// <summary>
    /// Runs one extraction per output path: callers that arrive while it runs (clients poll)
    /// share it instead of queueing their own.
    /// </summary>
    private static Task RunOnce(string outputPath, Func<Task> extract) =>
        RunningExtractions
            .GetOrAdd(outputPath, key => new Lazy<Task>(() => RunAndForgetAsync(key, extract)))
            .Value;

    private static async Task RunAndForgetAsync(string outputPath, Func<Task> extract)
    {
        try
        {
            await extract();
        }
        finally
        {
            RunningExtractions.TryRemove(outputPath, out _);
        }
    }

    private static async Task ExtractAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        string vttCachePath,
        ILogger logger)
    {
        if (IsReady(vttCachePath))
            return;

        // ffmpeg buffers its output: written in place, a large track would be readable (and
        // served) half written. Extract next to the target, then publish with a rename.
        var tempPath = CreateTempPath(vttCachePath);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

            await mediaTranscoder.ExtractSubtitleAsVttAsync(
                inputPath,
                trackIndex,
                tempPath,
                CancellationToken.None);

            if (!TryPublish(tempPath, vttCachePath))
            {
                logger.LogWarning(
                    "FFmpeg produced empty VTT for track {Track} from {Input} - nothing cached",
                    trackIndex,
                    inputPath);
                return;
            }

            if (stopwatch.Elapsed > SlowExtractionThreshold)
            {
                logger.LogWarning(
                    "Subtitle extraction of track {Track} from {Input} took {ElapsedSeconds} s. Until it ends, clients only get the cues around the playback position. The library capability that pre-extracts subtitles at scan time avoids this wait",
                    trackIndex,
                    inputPath,
                    (int)stopwatch.Elapsed.TotalSeconds);
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
            TryDelete(tempPath);
        }
    }

    private static async Task ExtractWindowAsync(
        IMediaTranscoder mediaTranscoder,
        string inputPath,
        int trackIndex,
        int windowIndex,
        string windowCachePath,
        ILogger logger)
    {
        if (File.Exists(windowCachePath))
            return;

        var windowStart = windowIndex * (double)WindowSeconds;
        var tempPath = CreateTempPath(windowCachePath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

            await mediaTranscoder.ExtractSubtitleWindowAsVttAsync(
                inputPath,
                trackIndex,
                TimeSpan.FromSeconds(Math.Max(0, windowStart - WindowLookBehindSeconds)),
                TimeSpan.FromSeconds(windowStart + WindowSeconds),
                tempPath,
                CancellationToken.None);

            // A window without dialogue is a valid result: ffmpeg still writes the header.
            if (File.Exists(tempPath))
                File.Move(tempPath, windowCachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Subtitle window {Window} extraction failed for track {Track} from {Input}",
                windowIndex,
                trackIndex,
                inputPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static string CreateTempPath(string vttCachePath) =>
        $"{vttCachePath}.{Guid.NewGuid():N}.tmp";

    private static bool TryPublish(string tempPath, string vttCachePath)
    {
        if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            return false;

        File.Move(tempPath, vttCachePath, overwrite: true);
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Left for the cache cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the cache cleanup.
        }
    }
}
