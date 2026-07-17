using System.Collections.Concurrent;
using System.Diagnostics;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using K7.Server.Application.Common.Configuration;

namespace K7.Server.Infrastructure.MediaProcessing;

public class TranscodeJobManager(
    ILogger<TranscodeJobManager> logger,
    IMediaTranscoder mediaTranscoder,
    IOptions<PathsConfiguration> pathsOptions,
    ITranscodeSettingsProvider transcodeSettingsProvider) : ITranscodeJobManager
{
    private readonly ConcurrentDictionary<Guid, TranscodeJob> _activeJobs = new();
    private readonly SemaphoreSlim _jobLock = new(1, 1);
    private readonly PathsConfiguration _pathsConfig = pathsOptions.Value;

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
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        EnsureTempQuotaAvailable(settings.TranscodeTempQuotaMb);

        var jobKey = GenerateJobKey(indexedFileId, quality, videoCodec ?? "copy", audioCodec ?? "copy", audioTrackIndex, isAudioOnly, subtitleBurnInStreamIndex);

        if (_activeJobs.TryGetValue(jobKey, out var existingJob))
        {
            existingJob.AttachedStreamSessions.TryAdd(streamSessionId, 0);
            existingJob.LastPingTime = DateTime.UtcNow;
            logger.LogInformation(
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
                existingJob.AttachedStreamSessions.TryAdd(streamSessionId, 0);
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

            if (Directory.Exists(outputDir))
            {
                foreach (var oldFile in Directory.EnumerateFiles(outputDir, "*.m4s"))
                {
                    try
                    {
                        File.Delete(oldFile);
                    }
                    catch (IOException ex)
                    {
                        logger.LogWarning(ex, "Failed to delete stale segment {File}", oldFile);
                    }
                }

                logger.LogInformation("Cleared stale segments from {OutputDir}", outputDir);
            }

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
                TargetSegmentIndex = 0,
                BufferSize = Math.Max(settings.EncoderThrottleBufferSegments, 1)
            };

            job.AttachedStreamSessions.TryAdd(streamSessionId, 0);
            _activeJobs[jobKey] = job;

            logger.LogInformation(
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
            job.AttachedStreamSessions.TryAdd(streamSessionId, 0);
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

        logger.LogDebug(
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
            // Re-check after acquiring lock - another request may have already started FFmpeg
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
                logger.LogInformation(
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
                logger.LogInformation(
                    "Job {JobId}: Backward seek detected (gap {Gap}), but segment {RequestedIndex} doesn't exist, restarting with seek",
                    jobId,
                    gap,
                    requestedSegmentIndex);

                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(job, startSegmentIndex, allSegments, cancellationToken);
            }
            else if (requestedSegmentIndex >= job.TargetSegmentIndex
                     || requestedSegmentIndex > job.GeneratingUntilSegmentIndex
                     || job.FfmpegTask == null
                     || job.FfmpegTask.IsCompleted)
            {
                if (job.FfmpegTask is { IsCompleted: true, IsFaulted: true })
                {
                    var fault = job.FfmpegTask.Exception?.GetBaseException();
                    logger.LogError(fault, "Job {JobId}: ffmpeg task faulted", jobId);
                    job.FfmpegTask = null;
                }

                // Extend the target or start/restart ffmpeg
                var newTarget = Math.Max(job.TargetSegmentIndex, requestedSegmentIndex + job.BufferSize);

                if (newTarget != job.TargetSegmentIndex || job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
                {
                    logger.LogInformation(
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
            job.AttachedStreamSessions.TryRemove(streamSessionId, out _);
            logger.LogInformation(
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
            .Where(j => (now - j.LastPingTime) > staleThreshold)
            .ToList();

        foreach (var job in staleJobs)
        {
            logger.LogInformation(
                "Cleaning up stale job {JobId} (last ping: {LastPing})",
                job.JobId,
                job.LastPingTime);

            if (job.FfmpegCancellation != null && !job.FfmpegCancellation.IsCancellationRequested)
            {
                try
                {
                    await StopFfmpegAsync(job);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to cancel ffmpeg task for job {JobId}", job.JobId);
                }
            }

            _activeJobs.TryRemove(job.JobId, out _);

            if (Directory.Exists(job.OutputDirectory))
            {
                try
                {
                    Directory.Delete(job.OutputDirectory, recursive: true);
                    logger.LogInformation("Deleted output directory for stale job {JobId}: {Dir}", job.JobId, job.OutputDirectory);

                    var parentDir = Path.GetDirectoryName(job.OutputDirectory);
                    if (parentDir is not null)
                    {
                        try
                        {
                            Directory.Delete(parentDir);
                            logger.LogInformation("Deleted empty parent directory for stale job {JobId}: {Dir}", job.JobId, parentDir);
                        }
                        catch (IOException)
                        {
                            // Parent still has other entries or raced with another job.
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete output directory for stale job {JobId}: {Dir}", job.JobId, job.OutputDirectory);
                }
            }
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
        await StopFfmpegAsync(job);

        PurgeGeneratedSegments(job.OutputDirectory);

        job.TargetSegmentIndex = startSegmentIndex + job.BufferSize;

        await StartFfmpegAsync(job, startSegmentIndex, allSegments, cancellationToken);
    }

    private void PurgeGeneratedSegments(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(outputDirectory, "*.m4s"))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Failed to delete stale segment {File}", file);
            }
        }
    }

    private async Task ContinueJobAsync(
        TranscodeJob job,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        var currentIndex = job.GetCurrentSegmentIndex();
        var startIndex = currentIndex + 1;

        logger.LogInformation(
            "Job {JobId}: ContinueJobAsync - currentIndex={CurrentIndex}, startIndex={StartIndex}, targetIndex={TargetIndex}, totalSegments={TotalSegments}",
            job.JobId,
            currentIndex,
            startIndex,
            job.TargetSegmentIndex,
            allSegments.Count);

        if (startIndex >= allSegments.Count)
        {
            logger.LogInformation("Job {JobId}: All segments already generated", job.JobId);
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
            logger.LogWarning(
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

        // Slot wait may use the request token, but ffmpeg itself must NOT - otherwise a
        // client abort/timeout on one segment request kills generation for everyone.
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        await WaitForTranscodeSlotAsync(settings.MaxConcurrentTranscodes, cancellationToken);

        logger.LogInformation(
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
            job.FfmpegCancellation?.Dispose();
            job.FfmpegCancellation = new CancellationTokenSource();
            var ffmpegToken = job.FfmpegCancellation.Token;
            job.GeneratingUntilSegmentIndex = endSegmentIndex - 1;

            // Start ffmpeg in background, owned only by the job lifetime
            job.FfmpegTask = Task.Run(async () =>
            {
                if (job.IsAudioOnly)
                {
                    await mediaTranscoder.StartAudioStreamingTranscodeAsync(
                        job.InputFilePath,
                        job.OutputDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        ffmpegToken,
                        job.AudioTrackIndex,
                        audioCodec);
                }
                else
                {
                    await mediaTranscoder.StartVideoStreamingTranscodeAsync(
                        job.InputFilePath,
                        job.OutputDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        ffmpegToken,
                        videoCodec,
                        job.Quality,
                        job.SubtitleBurnInStreamIndex);
                }
            }, CancellationToken.None);

            logger.LogInformation(
                "Job {JobId}: ffmpeg task started in background",
                job.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId}: Failed to start ffmpeg task", job.JobId);
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

    private async Task StopFfmpegAsync(TranscodeJob job)
    {
        if (job.FfmpegCancellation is null)
            return;

        try
        {
            if (!job.FfmpegCancellation.IsCancellationRequested)
                job.FfmpegCancellation.Cancel();

            if (job.FfmpegTask is not null)
            {
                try
                {
                    await job.FfmpegTask;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "ffmpeg task ended with error for job {JobId}", job.JobId);
                }
            }
        }
        finally
        {
            job.FfmpegCancellation.Dispose();
            job.FfmpegCancellation = null;
            job.FfmpegTask = null;
            job.GeneratingUntilSegmentIndex = -1;
        }
    }

    private void EnsureTempQuotaAvailable(int quotaMb)
    {
        if (quotaMb <= 0)
            return;

        var usedMb = GetTranscodingDirectorySizeMb();
        if (usedMb >= quotaMb)
            throw new InvalidOperationException("Transcode temporary storage quota exceeded.");
    }

    private long GetTranscodingDirectorySizeMb()
    {
        var transcodingPath = _pathsConfig.Transcoding;
        if (string.IsNullOrEmpty(transcodingPath) || !Directory.Exists(transcodingPath))
            return 0;

        long totalBytes = 0;
        foreach (var file in Directory.EnumerateFiles(transcodingPath, "*", SearchOption.AllDirectories))
        {
            totalBytes += new FileInfo(file).Length;
        }

        return totalBytes / (1024 * 1024);
    }

    private async Task WaitForTranscodeSlotAsync(int maxConcurrent, CancellationToken cancellationToken)
    {
        if (maxConcurrent <= 0)
            return;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var running = _activeJobs.Values.Count(j => j.FfmpegTask is { IsCompleted: false });
            if (running < maxConcurrent)
                return;

            await Task.Delay(500, cancellationToken);
        }
    }
}
