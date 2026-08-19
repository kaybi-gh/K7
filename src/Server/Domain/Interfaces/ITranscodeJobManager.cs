using System.Collections.Concurrent;
using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Interfaces;

public interface ITranscodeJobManager
{
    /// <summary>
    /// Gets an existing transcode job or starts a new one for the specified parameters.
    /// When <paramref name="isAudioOnly"/> is true, creates an audio-only job keyed by audio codec and track index.
    /// When false, creates a video-only job keyed by quality and video codec.
    /// </summary>
    Task<TranscodeJob> GetOrStartJobAsync(
        Guid indexedFileId,
        string inputFilePath,
        string quality,
        string? videoCodec,
        string? audioCodec,
        int audioTrackIndex,
        bool isAudioOnly,
        Guid streamSessionId,
        CancellationToken cancellationToken = default,
        int? subtitleBurnInStreamIndex = null);

    /// <summary>
    /// Signals that a session is still actively using this job.
    /// </summary>
    void PingJob(Guid jobId, Guid streamSessionId);

    /// <summary>
    /// Ensures that the requested segment will be generated, potentially restarting
    /// ffmpeg with seek if the gap is too large, or extending the target if reasonable.
    /// Pass <paramref name="requestedSegmentIndex"/> = -1 for init.m4s (never treat as media segment 0).
    /// </summary>
    Task EnsureSegmentWillBeGeneratedAsync(
        Guid jobId,
        int requestedSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches a stream session from a job. If no sessions remain, the job may be cleaned up.
    /// </summary>
    void DetachSession(Guid jobId, Guid streamSessionId);

    /// <summary>
    /// Cleans up stale jobs that have no attached sessions for more than the specified duration.
    /// Also removes leftover sidecar subtitle cache under the same indexed-file folder.
    /// </summary>
    Task CleanupStaleJobsAsync(TimeSpan staleThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current segment index (last completed segment) for a job.
    /// </summary>
    int GetCurrentSegmentIndex(Guid jobId);

    /// <summary>
    /// Finds an active video (non-audio) transcode job for the indexed file, if any.
    /// Used to soft-gate demuxed audio until video init.m4s is ready.
    /// </summary>
    TranscodeJob? FindVideoJobForIndexedFile(Guid indexedFileId);
}

public class TranscodeJob
{
    public required Guid JobId { get; init; }
    public required Guid IndexedFileId { get; init; }
    public required string Quality { get; init; }
    public required string? VideoCodec { get; init; }
    public required string? AudioCodec { get; init; }
    public required int AudioTrackIndex { get; init; }
    public required bool IsAudioOnly { get; init; }
    public int? SubtitleBurnInStreamIndex { get; init; }
    public required string OutputDirectory { get; init; }
    public required string InputFilePath { get; init; }
    
    public CancellationTokenSource? FfmpegCancellation { get; set; }
    public Task? FfmpegTask { get; set; }
    public ConcurrentDictionary<Guid, byte> AttachedStreamSessions { get; } = new();
    public DateTime LastPingTime { get; set; } = DateTime.UtcNow;
    public int TargetSegmentIndex { get; set; }
    /// <summary>
    /// Highest segment index the currently running ffmpeg process will produce (inclusive).
    /// </summary>
    public int GeneratingUntilSegmentIndex { get; set; } = -1;
    public int BufferSize { get; init; } = 10;

    /// <summary>
    /// Per-job lock to prevent concurrent FFmpeg process starts from parallel segment requests.
    /// </summary>
    public SemaphoreSlim FfmpegStartLock { get; } = new(1, 1);
    
    /// <summary>
    /// Highest contiguous ready media segment index in the output directory.
    /// Mid-seek windows start at N (not 0); never report N-1 when N is present but unready
    /// (that caused infinite ContinueJob regenerating the same window).
    /// </summary>
    public int GetCurrentSegmentIndex()
    {
        if (!Directory.Exists(OutputDirectory))
            return -1;

        var indices = Directory.GetFiles(OutputDirectory, "*.m4s")
            .Select(static f =>
            {
                var fileName = Path.GetFileNameWithoutExtension(f);
                if (string.Equals(fileName, "init", StringComparison.OrdinalIgnoreCase))
                    return -1;

                return int.TryParse(fileName, out var segmentIndex) ? segmentIndex : -1;
            })
            .Where(static n => n >= 0)
            .Distinct()
            .OrderBy(static n => n)
            .ToList();

        if (indices.Count == 0)
            return -1;

        // Skip leading unready placeholders, then take the contiguous ready run.
        var i = 0;
        while (i < indices.Count
               && !IsReadyMediaSegment(Path.Combine(OutputDirectory, $"{indices[i]}.m4s")))
        {
            i++;
        }

        if (i >= indices.Count)
            return -1;

        var current = indices[i];
        var expected = current + 1;
        for (var j = i + 1; j < indices.Count; j++)
        {
            if (indices[j] != expected)
                break;

            if (!IsReadyMediaSegment(Path.Combine(OutputDirectory, $"{indices[j]}.m4s")))
                break;

            current = indices[j];
            expected++;
        }

        return current;
    }

    private static bool IsReadyMediaSegment(string path)
    {
        try
        {
            var length = new FileInfo(path).Length;
            // Full fMP4 validation lives in Application; here only reject empty/tiny placeholders
            // so Domain stays dependency-free.
            return length >= 32;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
