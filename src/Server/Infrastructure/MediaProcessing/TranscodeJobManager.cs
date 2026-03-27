using System.Collections.Concurrent;
using System.Diagnostics;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using K7.Server.Application.Common.Configuration;

namespace K7.Server.Infrastructure.MediaProcessing;

public class TranscodeJobManager : ITranscodeJobManager
{
    private readonly ConcurrentDictionary<Guid, TranscodeJob> _activeJobs = new();
    private readonly SemaphoreSlim _jobLock = new(1, 1);
    private readonly ILogger<TranscodeJobManager> _logger;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly PathsConfiguration _pathsConfig;

    public TranscodeJobManager(
        ILogger<TranscodeJobManager> logger,
        IMediaTranscoder mediaTranscoder,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _logger = logger;
        _mediaTranscoder = mediaTranscoder;
        _pathsConfig = pathsOptions.Value;
    }

    public async Task<TranscodeJob> GetOrStartJobAsync(
        Guid indexedFileId,
        string inputFilePath,
        string quality,
        string? videoCodec,
        string? audioCodec,
        int audioTrackIndex,
        bool isAudioOnly,
        Guid streamSessionId,
        CancellationToken cancellationToken = default,
        int? subtitleBurnInStreamIndex = null)
    {
        var jobKey = GenerateJobKey(indexedFileId, quality, videoCodec ?? "copy", audioCodec ?? "copy", audioTrackIndex, isAudioOnly, subtitleBurnInStreamIndex);

        if (_activeJobs.TryGetValue(jobKey, out var existingJob))
        {
            existingJob.AttachedStreamSessions.Add(streamSessionId);
            existingJob.LastPingTime = DateTime.UtcNow;
            _logger.LogInformation(
                "Reusing existing transcode job {JobId} for session {SessionId}",
                existingJob.JobId,
                streamSessionId);
            return existingJob;
        }

        await _jobLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_activeJobs.TryGetValue(jobKey, out existingJob))
            {
                existingJob.AttachedStreamSessions.Add(streamSessionId);
                existingJob.LastPingTime = DateTime.UtcNow;
                return existingJob;
            }

            // Create new job - note: we don't start ffmpeg here yet
            // It will be started on-demand in EnsureSegmentWillBeGeneratedAsync
            var transcodingPath = _pathsConfig.Transcoding ?? throw new InvalidOperationException("Transcoding path not configured");
            var videoSubDir = subtitleBurnInStreamIndex.HasValue
                ? $"video-{quality}-{videoCodec ?? "copy"}-sub{subtitleBurnInStreamIndex.Value}"
                : $"video-{quality}-{videoCodec ?? "copy"}";

            var outputDir = isAudioOnly
                ? Path.Combine(transcodingPath, indexedFileId.ToString("N"), $"audio-{audioCodec ?? "copy"}-a{audioTrackIndex}")
                : Path.Combine(transcodingPath, indexedFileId.ToString("N"), videoSubDir);

            Directory.CreateDirectory(outputDir);

            var job = new TranscodeJob
            {
                JobId = jobKey,
                IndexedFileId = indexedFileId,
                Quality = quality,
                VideoCodec = videoCodec,
                AudioCodec = audioCodec,
                AudioTrackIndex = audioTrackIndex,
                IsAudioOnly = isAudioOnly,
                SubtitleBurnInStreamIndex = subtitleBurnInStreamIndex,
                OutputDirectory = outputDir,
                InputFilePath = inputFilePath,
                TargetSegmentIndex = 0
            };

            job.AttachedStreamSessions.Add(streamSessionId);
            _activeJobs[jobKey] = job;

            _logger.LogInformation(
                "Created new transcode job {JobId} for IndexedFile {IndexedFileId}, Quality {Quality}, OutputDir: {OutputDir}",
                job.JobId,
                indexedFileId,
                quality,
                outputDir);

            return job;
        }
        finally
        {
            _jobLock.Release();
        }
    }

    public void PingJob(Guid jobId, Guid streamSessionId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.LastPingTime = DateTime.UtcNow;
            job.AttachedStreamSessions.Add(streamSessionId);
        }
    }

    public async Task EnsureSegmentWillBeGeneratedAsync(
        Guid jobId,
        int requestedSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken = default)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job))
        {
            throw new InvalidOperationException($"Job {jobId} not found");
        }

        var currentIndex = job.GetCurrentSegmentIndex();
        var gap = requestedSegmentIndex - currentIndex;

        // Check if requested segment already exists on disk (for backward seeks)
        var segmentPath = Path.Combine(job.OutputDirectory, $"{requestedSegmentIndex}.m4s");
        var segmentExists = File.Exists(segmentPath);

        // Calculate gap duration
        var gapDuration = CalculateGapDuration(currentIndex, requestedSegmentIndex, allSegments);

        _logger.LogDebug(
            "Job {JobId}: Requested segment {RequestedIndex}, current {CurrentIndex}, gap {Gap} segments ({GapSeconds}s), exists: {Exists}",
            jobId,
            requestedSegmentIndex,
            currentIndex,
            gap,
            gapDuration.TotalSeconds,
            segmentExists);

        // If segment exists, nothing to do
        if (segmentExists)
        {
            return;
        }

        // Acquire per-job lock to prevent concurrent FFmpeg starts from parallel segment requests
        await job.FfmpegStartLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring lock — another request may have already started FFmpeg
            segmentExists = File.Exists(segmentPath);
            if (segmentExists)
            {
                return;
            }

            currentIndex = job.GetCurrentSegmentIndex();
            gap = requestedSegmentIndex - currentIndex;

            // Restart threshold: 30 segments or 60 seconds
            if (gap > 30 || gapDuration.TotalSeconds > 60)
            {
                _logger.LogInformation(
                    "Job {JobId}: Gap too large ({Gap} segments, {GapSeconds}s), restarting with seek",
                    jobId,
                    gap,
                    gapDuration.TotalSeconds);

                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(job, startSegmentIndex, allSegments, cancellationToken);
            }
            else if (gap < 0)
            {
                // Backward seek: segment should exist but doesn't, restart with seek
                _logger.LogInformation(
                    "Job {JobId}: Backward seek detected (gap {Gap}), but segment {RequestedIndex} doesn't exist, restarting with seek",
                    jobId,
                    gap,
                    requestedSegmentIndex);

                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(job, startSegmentIndex, allSegments, cancellationToken);
            }
            else if (requestedSegmentIndex >= job.TargetSegmentIndex || job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
            {
                // Extend the target or start/restart ffmpeg
                var newTarget = Math.Max(job.TargetSegmentIndex, requestedSegmentIndex + job.BufferSize);

                if (newTarget != job.TargetSegmentIndex || job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
                {
                    _logger.LogInformation(
                        "Job {JobId}: Extending target from {OldTarget} to {NewTarget} (ffmpeg running: {FfmpegRunning})",
                        jobId,
                        job.TargetSegmentIndex,
                        newTarget,
                        job.FfmpegTask != null && !job.FfmpegTask.IsCompleted);

                    job.TargetSegmentIndex = newTarget;

                    // If ffmpeg has finished or not started, continue/start
                    if (job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
                    {
                        await ContinueJobAsync(job, allSegments, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            job.FfmpegStartLock.Release();
        }
    }

    public void DetachSession(Guid jobId, Guid streamSessionId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.AttachedStreamSessions.Remove(streamSessionId);
            _logger.LogInformation(
                "Detached session {SessionId} from job {JobId}. Remaining sessions: {Count}",
                streamSessionId,
                jobId,
                job.AttachedStreamSessions.Count);
        }
    }

    public async Task CleanupStaleJobsAsync(TimeSpan staleThreshold, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleJobs = _activeJobs.Values
            .Where(j => j.AttachedStreamSessions.Count == 0 && (now - j.LastPingTime) > staleThreshold)
            .ToList();

        foreach (var job in staleJobs)
        {
            _logger.LogInformation(
                "Cleaning up stale job {JobId} (last ping: {LastPing})",
                job.JobId,
                job.LastPingTime);

            if (job.FfmpegCancellation != null && !job.FfmpegCancellation.IsCancellationRequested)
            {
                try
                {
                    job.FfmpegCancellation.Cancel();
                    if (job.FfmpegTask != null)
                    {
                        await job.FfmpegTask;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel ffmpeg task for job {JobId}", job.JobId);
                }
            }

            _activeJobs.TryRemove(job.JobId, out _);

            // TODO - Optionally delete segments (for now, keep them for potential reuse)
        }

        await Task.CompletedTask;
    }

    public int GetCurrentSegmentIndex(Guid jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            return job.GetCurrentSegmentIndex();
        }
        return -1;
    }

    private async Task RestartJobWithSeekAsync(
        TranscodeJob job,
        int startSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        // Kill existing process
        if (job.FfmpegCancellation != null && !job.FfmpegCancellation.IsCancellationRequested)
        {
            try
            {
                job.FfmpegCancellation.Cancel();
                if (job.FfmpegTask != null)
                {
                    await job.FfmpegTask;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling ffmpeg task for job {JobId}", job.JobId);
            }
        }

        // Update target
        job.TargetSegmentIndex = startSegmentIndex + job.BufferSize;

        // Start from the requested position
        await StartFfmpegAsync(job, startSegmentIndex, allSegments, cancellationToken);
    }

    private async Task ContinueJobAsync(
        TranscodeJob job,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        var currentIndex = job.GetCurrentSegmentIndex();
        var startIndex = currentIndex + 1;

        _logger.LogInformation(
            "Job {JobId}: ContinueJobAsync - currentIndex={CurrentIndex}, startIndex={StartIndex}, targetIndex={TargetIndex}, totalSegments={TotalSegments}",
            job.JobId,
            currentIndex,
            startIndex,
            job.TargetSegmentIndex,
            allSegments.Count);

        if (startIndex >= allSegments.Count)
        {
            _logger.LogInformation("Job {JobId}: All segments already generated", job.JobId);
            return;
        }

        await StartFfmpegAsync(job, startIndex, allSegments, cancellationToken);
    }

    private async Task StartFfmpegAsync(
        TranscodeJob job,
        int startSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        if (startSegmentIndex < 0 || startSegmentIndex >= allSegments.Count)
        {
            _logger.LogWarning(
                "Job {JobId}: Invalid start segment index {Index} (total segments: {Total})",
                job.JobId,
                startSegmentIndex,
                allSegments.Count);
            return;
        }

        var segmentsToGenerate = job.TargetSegmentIndex - startSegmentIndex + 1;
        if (segmentsToGenerate <= 0)
        {
            return;
        }

        _logger.LogInformation(
            "Job {JobId}: Starting ffmpeg from segment {Start} to {Target} ({Count} segments)",
            job.JobId,
            startSegmentIndex,
            job.TargetSegmentIndex,
            segmentsToGenerate);

        // Determine video and audio codecs for transcoding
        var videoCodec = job.VideoCodec != "copy" ? job.VideoCodec : null;
        var audioCodec = job.AudioCodec != "copy" ? job.AudioCodec : null;
        var endSegmentIndex = Math.Min(job.TargetSegmentIndex + 1, allSegments.Count);

        try
        {
            // Create a cancellation token for this specific ffmpeg task
            job.FfmpegCancellation = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.FfmpegCancellation.Token);

            // Start ffmpeg in background
            job.FfmpegTask = Task.Run(async () =>
            {
                if (job.IsAudioOnly)
                {
                    await _mediaTranscoder.StartAudioStreamingTranscodeAsync(
                        job.InputFilePath,
                        job.OutputDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        linkedCts.Token,
                        job.AudioTrackIndex,
                        audioCodec);
                }
                else
                {
                    await _mediaTranscoder.StartVideoStreamingTranscodeAsync(
                        job.InputFilePath,
                        job.OutputDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        linkedCts.Token,
                        videoCodec,
                        job.Quality,
                        job.SubtitleBurnInStreamIndex);
                }
            }, linkedCts.Token);

            _logger.LogInformation(
                "Job {JobId}: ffmpeg task started in background",
                job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId}: Failed to start ffmpeg task", job.JobId);
            throw;
        }
    }

    private static Guid GenerateJobKey(Guid indexedFileId, string quality, string videoCodec, string audioCodec, int audioTrackIndex, bool isAudioOnly, int? subtitleBurnInStreamIndex = null)
    {
        var subtitleSuffix = subtitleBurnInStreamIndex.HasValue ? $"|sub{subtitleBurnInStreamIndex.Value}" : "";
        var keyString = isAudioOnly
            ? $"{indexedFileId}|audio|{audioCodec}|a{audioTrackIndex}"
            : $"{indexedFileId}|video|{quality}|{videoCodec}{subtitleSuffix}";
        return Guid.Parse(System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(keyString))
            .Take(16).ToArray().Aggregate("", (s, b) => s + b.ToString("x2")));
    }

    private static TimeSpan CalculateGapDuration(int fromIndex, int toIndex, List<HlsSegment> allSegments)
    {
        // toIndex -1 means init.m4s request, no gap calculation needed
        if (toIndex < 0 || toIndex >= allSegments.Count)
        {
            return TimeSpan.Zero;
        }

        if (fromIndex >= 0 && fromIndex >= toIndex)
        {
            return TimeSpan.Zero;
        }

        if (fromIndex >= allSegments.Count)
        {
            return TimeSpan.Zero;
        }

        // If fromIndex is -1 (no segments generated yet), calculate from start
        var startTimestamp = fromIndex >= 0 ? allSegments[fromIndex].StartTimestamp : 0;
        var endTimestamp = allSegments[toIndex].StartTimestamp;

        return TimeSpan.FromMilliseconds(endTimestamp - startTimestamp);
    }
}
