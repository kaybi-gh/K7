using System.Diagnostics;
using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Interfaces;

public interface ITranscodeJobManager
{
    /// <summary>
    /// Gets an existing transcode job or starts a new one for the specified parameters.
    /// </summary>
    Task<TranscodeJob> GetOrStartJobAsync(
        Guid indexedFileId,
        string inputFilePath,
        string quality,
        string? videoCodec,
        string? audioCodec,
        Guid streamSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that a session is still actively using this job.
    /// </summary>
    void PingJob(Guid jobId, Guid streamSessionId);

    /// <summary>
    /// Ensures that the requested segment will be generated, potentially restarting
    /// ffmpeg with seek if the gap is too large, or extending the target if reasonable.
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
    /// </summary>
    Task CleanupStaleJobsAsync(TimeSpan staleThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current segment index (last completed segment) for a job.
    /// </summary>
    int GetCurrentSegmentIndex(Guid jobId);
}

public class TranscodeJob
{
    public required Guid JobId { get; init; }
    public required Guid IndexedFileId { get; init; }
    public required string Quality { get; init; }
    public required string? VideoCodec { get; init; }
    public required string? AudioCodec { get; init; }
    public required string OutputDirectory { get; init; }
    public required string InputFilePath { get; init; }
    
    public Process? FfmpegProcess { get; set; }
    public CancellationTokenSource? FfmpegCancellation { get; set; }
    public Task? FfmpegTask { get; set; }
    public HashSet<Guid> AttachedStreamSessions { get; } = new();
    public DateTime LastPingTime { get; set; } = DateTime.UtcNow;
    public int TargetSegmentIndex { get; set; }
    public int BufferSize { get; init; } = 15;
    
    /// <summary>
    /// Gets the index of the last segment that has been completely written to disk.
    /// </summary>
    public int GetCurrentSegmentIndex()
    {
        if (!Directory.Exists(OutputDirectory))
        {
            return -1;
        }

        var segmentFiles = Directory.GetFiles(OutputDirectory, "segment_*.m4s")
            .Select(f =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    Path.GetFileName(f),
                    @"segment_(\d+)\.m4s");
                return match.Success ? int.Parse(match.Groups[1].Value) : -1;
            })
            .Where(n => n >= 0)
            .OrderByDescending(n => n);

        return segmentFiles.FirstOrDefault(-1);
    }
}
