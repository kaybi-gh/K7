using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.MediaProcessing;

public class TranscodeJobManager(
    ILogger<TranscodeJobManager> logger,
    IMediaTranscoder mediaTranscoder,
    IOptions<PathsConfiguration> pathsOptions,
    ITranscodeSettingsProvider transcodeSettingsProvider,
    IServiceScopeFactory scopeFactory) : ITranscodeJobManager
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
            logger.LogDebug(
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

            var outputDir = FfmpegStreamingArgs.NormalizeOutputDirectory(isAudioOnly
                ? Path.Combine(transcodingPath, indexedFileId.ToString("N"), $"audio-{audioCodec ?? "copy"}-a{audioTrackIndex}")
                : Path.Combine(transcodingPath, indexedFileId.ToString("N"), videoSubDir));

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

                foreach (var headDir in Directory.EnumerateDirectories(outputDir, "head-*"))
                {
                    try
                    {
                        Directory.Delete(headDir, recursive: true);
                    }
                    catch (IOException ex)
                    {
                        logger.LogWarning(ex, "Failed to delete stale remux head {Dir}", headDir);
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

        // init.m4s: never map to media segment 0 (that false-triggers seek-to-start on resume).
        if (requestedSegmentIndex < 0)
        {
            await EnsureInitWillBeGeneratedAsync(job, allSegments, cancellationToken);
            return;
        }

        job.LastClientMediaSegmentRequest = requestedSegmentIndex;
        job.LastRequestedSegmentIndex = Math.Max(job.LastRequestedSegmentIndex, requestedSegmentIndex);

        // Advertise the real target early so a racing init request does not assume cold start at 0.
        job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
            requestedSegmentIndex,
            job.TargetSegmentIndex,
            job.BufferSize,
            allSegments.Count,
            job.IsCopyRemux);

        if (job.IsCopyRemux)
        {
            await EnsureRemuxSegmentWillBeGeneratedAsync(job, requestedSegmentIndex, allSegments, cancellationToken);
            return;
        }

        await EnsureEncodeSegmentWillBeGeneratedAsync(job, requestedSegmentIndex, allSegments, cancellationToken);
    }

    private async Task EnsureRemuxSegmentWillBeGeneratedAsync(
        TranscodeJob job,
        int requestedSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        var segmentExists = HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, requestedSegmentIndex);
        logger.LogDebug(
            "Job {JobId}: Remux requested segment {RequestedIndex}, exists: {Exists}, heads: {HeadCount}",
            job.JobId,
            requestedSegmentIndex,
            segmentExists,
            job.RemuxHeads.Count);

        if (segmentExists)
        {
            await job.FfmpegStartLock.WaitAsync(cancellationToken);
            try
            {
                await ContinueTowardClientTargetIfNeededAsync(job, allSegments, cancellationToken);
            }
            finally
            {
                job.FfmpegStartLock.Release();
            }

            return;
        }

        await job.FfmpegStartLock.WaitAsync(cancellationToken);
        try
        {
            segmentExists = HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, requestedSegmentIndex);
            if (segmentExists)
            {
                await ContinueTowardClientTargetIfNeededAsync(job, allSegments, cancellationToken);
                return;
            }

            var liveHeads = job.RemuxHeads.Values
                .Select(h => (TipIndex: h.TipIndex, From: h.From, UntilInclusive: h.UntilInclusive, Running: h.IsRunning))
                .ToList();
            var covered = liveHeads.Any(h =>
                h.Running && requestedSegmentIndex >= h.From && requestedSegmentIndex <= h.UntilInclusive);
            var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
                requestedSegmentIndex,
                liveHeads.Select(h => (h.TipIndex, h.UntilInclusive, h.Running)),
                allSegments);

            if (!FfmpegRemuxSeekPolicy.ShouldSpawnRemuxHead(
                    remuxCopy: true,
                    segmentReady: false,
                    covered,
                    distance))
            {
                logger.LogDebug(
                    "Job {JobId}: Waiting on remux head for segment {RequestedIndex} (covered={Covered}, distance={Distance}s)",
                    job.JobId,
                    requestedSegmentIndex,
                    covered,
                    distance);
                return;
            }

            logger.LogInformation(
                "Job {JobId}: Spawning remux head at segment {RequestedIndex} (covered={Covered}, distance={Distance}s)",
                job.JobId,
                requestedSegmentIndex,
                covered,
                distance);
            await SpawnRemuxHeadAsync(job, requestedSegmentIndex, allSegments, cancellationToken);
        }
        finally
        {
            job.FfmpegStartLock.Release();
        }
    }

    private async Task EnsureEncodeSegmentWillBeGeneratedAsync(
        TranscodeJob job,
        int requestedSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        var currentIndex = job.GetCurrentSegmentIndex();
        var gap = requestedSegmentIndex - currentIndex;
        var segmentExists = HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, requestedSegmentIndex);
        var gapDuration = CalculateGapDuration(currentIndex, requestedSegmentIndex, allSegments);

        logger.LogDebug(
            "Job {JobId}: Encode requested segment {RequestedIndex}, current {CurrentIndex}, gap {Gap} segments ({GapSeconds}s), exists: {Exists}",
            job.JobId,
            requestedSegmentIndex,
            currentIndex,
            gap,
            gapDuration.TotalSeconds,
            segmentExists);

        if (segmentExists)
        {
            await job.FfmpegStartLock.WaitAsync(cancellationToken);
            try
            {
                await ContinueTowardClientTargetIfNeededAsync(job, allSegments, cancellationToken);
            }
            finally
            {
                job.FfmpegStartLock.Release();
            }

            return;
        }

        await job.FfmpegStartLock.WaitAsync(cancellationToken);
        try
        {
            segmentExists = HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, requestedSegmentIndex);
            if (segmentExists)
            {
                await ContinueTowardClientTargetIfNeededAsync(job, allSegments, cancellationToken);
                return;
            }

            currentIndex = job.GetCurrentSegmentIndex();
            gap = requestedSegmentIndex - currentIndex;
            var ffmpegRunning = job.FfmpegTask is { IsCompleted: false };

            var missingWithLaterSegments = HlsSegmentFileWaiter.HasReadyMediaSegmentAfter(
                job.OutputDirectory,
                requestedSegmentIndex);
            if (HlsSegmentGenerationPolicy.ShouldRestartForHole(
                    requestedSegmentIndex,
                    ffmpegRunning,
                    missingWithLaterSegments))
            {
                logger.LogInformation(
                    "Job {JobId}: Filling encode hole at segment {RequestedIndex}",
                    job.JobId,
                    requestedSegmentIndex);
                await RestartJobWithSeekAsync(
                    job,
                    requestedSegmentIndex,
                    allSegments,
                    cancellationToken,
                    purgeExisting: false);
                return;
            }

            if (gap > 30 || gapDuration.TotalSeconds > 60)
            {
                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(job, startSegmentIndex, allSegments, cancellationToken);
            }
            else if (gap < 0)
            {
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
                    logger.LogError(fault, "Job {JobId}: ffmpeg task faulted", job.JobId);
                    job.FfmpegTask = null;
                }

                var newTarget = FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
                    requestedSegmentIndex,
                    job.TargetSegmentIndex,
                    job.BufferSize,
                    allSegments.Count,
                    remuxToEnd: false);

                if (newTarget != job.TargetSegmentIndex || job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
                {
                    job.TargetSegmentIndex = newTarget;
                    if (job.FfmpegTask == null || job.FfmpegTask.IsCompleted)
                        await ContinueJobAsync(job, allSegments, cancellationToken);
                }
            }
        }
        finally
        {
            job.FfmpegStartLock.Release();
        }
    }

    private async Task SpawnRemuxHeadAsync(
        TranscodeJob job,
        int startSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        if (startSegmentIndex < 0 || startSegmentIndex >= allSegments.Count)
            return;

        job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
            startSegmentIndex,
            job.TargetSegmentIndex,
            job.BufferSize,
            allSegments.Count,
            remuxToEnd: true);

        var endSegmentIndex = Math.Min(job.TargetSegmentIndex + 1, allSegments.Count);
        var untilInclusive = endSegmentIndex - 1;
        var headId = job.AllocateRemuxHeadId();
        var stagingDirectory = Path.Combine(job.OutputDirectory, "head-" + headId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(stagingDirectory);

        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        await WaitForTranscodeSlotAsync(settings.MaxConcurrentTranscodes, cancellationToken);

        var cts = new CancellationTokenSource();
        var head = new TranscodeRemuxHead
        {
            Id = headId,
            From = startSegmentIndex,
            UntilInclusive = untilInclusive,
            StagingDirectory = stagingDirectory,
            Cancellation = cts,
            TipIndex = startSegmentIndex
        };
        job.RemuxHeads[headId] = head;
        job.RemuxRapSegmentIndices[startSegmentIndex] = 0;
        job.GeneratingFromSegmentIndex = startSegmentIndex;
        job.GeneratingUntilSegmentIndex = untilInclusive;

        var videoCodec = job.VideoCodec != "copy" ? job.VideoCodec : null;
        var audioCodec = job.AudioCodec != "copy" ? job.AudioCodec : null;

        var ffmpegTask = Task.Run(async () =>
        {
            var promoteTask = PromoteRemuxHeadLoopAsync(job, head, allSegments, cts.Token);
            try
            {
                if (job.IsAudioOnly)
                {
                    await mediaTranscoder.StartAudioStreamingTranscodeAsync(
                        job.InputFilePath,
                        stagingDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        cts.Token,
                        job.AudioTrackIndex,
                        audioCodec);
                }
                else
                {
                    await mediaTranscoder.StartVideoStreamingTranscodeAsync(
                        job.InputFilePath,
                        stagingDirectory,
                        allSegments,
                        startSegmentIndex,
                        endSegmentIndex,
                        cts.Token,
                        videoCodec,
                        job.Quality,
                        job.SubtitleBurnInStreamIndex);
                }
            }
            finally
            {
                try
                {
                    await cts.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    await promoteTask;
                }
                catch (OperationCanceledException)
                {
                }

                // Final promote pass after ffmpeg exits.
                PromoteRemuxHeadOnce(job, head);
            }
        }, CancellationToken.None);

        head.Task = ffmpegTask;
        // Compatibility for waiters that still inspect FfmpegTask.
        if (job.FfmpegTask is not { IsCompleted: false })
            job.FfmpegTask = ffmpegTask;

        _ = ffmpegTask.ContinueWith(t =>
        {
            job.RemuxHeads.TryRemove(headId, out _);
            try
            {
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            if (t.IsFaulted)
            {
                var fault = t.Exception?.GetBaseException();
                if (fault is not null and not OperationCanceledException)
                {
                    _ = PublishTranscodeFailedAsync(
                        job.IndexedFileId,
                        Path.GetFileName(job.InputFilePath),
                        fault.Message);
                }
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

        logger.LogInformation(
            "Job {JobId}: Remux head {HeadId} started at {From} until {Until} staging={Staging}",
            job.JobId,
            headId,
            startSegmentIndex,
            untilInclusive,
            stagingDirectory);
    }

    private async Task PromoteRemuxHeadLoopAsync(
        TranscodeJob job,
        TranscodeRemuxHead head,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PromoteRemuxHeadOnce(job, head);

            // Stop this head when the next shared segment is already ready.
            var next = head.TipIndex + 1;
            if (next <= head.UntilInclusive
                && next > head.From
                && HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, next)
                && !File.Exists(Path.Combine(head.StagingDirectory, next + ".m4s")))
            {
                logger.LogInformation(
                    "Job {JobId}: Stopping remux head {HeadId} because segment {Next} is already ready",
                    job.JobId,
                    head.Id,
                    next);
                try
                {
                    await head.Cancellation.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void PromoteRemuxHeadOnce(TranscodeJob job, TranscodeRemuxHead head)
    {
        // Promote closed media first so shared init can satisfy sibling-ready checks.
        for (var i = head.From; i <= head.UntilInclusive; i++)
        {
            var stagingPath = Path.Combine(
                head.StagingDirectory,
                i.ToString(CultureInfo.InvariantCulture) + ".m4s");
            if (!HlsSegmentFileWaiter.IsSegmentFileReady(stagingPath))
                continue;

            if (RemuxSegmentPromoter.TryPromoteMediaSegment(head.StagingDirectory, job.OutputDirectory, i))
            {
                job.RemuxSegmentOwners.TryAdd(i, head.Id);
                if (i >= head.TipIndex)
                    head.TipIndex = i;
            }
            else if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, i) && i >= head.TipIndex)
            {
                job.RemuxSegmentOwners.TryAdd(i, head.Id);
                head.TipIndex = i;
            }
        }

        var stagingInit = Path.Combine(head.StagingDirectory, HlsSegmentFileWaiter.InitSegmentFileName);
        if (HlsSegmentFileWaiter.IsSegmentFileReady(stagingInit)
            || File.Exists(stagingInit))
        {
            // Init may still be empty_moov until a media sibling exists; promote once media is shared.
            if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, head.From)
                || HlsSegmentFileWaiter.IsSegmentFileReady(
                    Path.Combine(head.StagingDirectory, head.From.ToString(CultureInfo.InvariantCulture) + ".m4s")))
            {
                RemuxSegmentPromoter.TryPromoteInit(head.StagingDirectory, job.OutputDirectory);
            }
        }
    }

    private async Task EnsureInitWillBeGeneratedAsync(
        TranscodeJob job,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        if (HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory))
            return;

        await job.FfmpegStartLock.WaitAsync(cancellationToken);
        try
        {
            if (HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory))
                return;

            // Active remux head or encode ffmpeg will write init.m4s; do not restart from segment 0.
            if (job.IsFfmpegRunning)
            {
                logger.LogDebug(
                    "Job {JobId}: Waiting for init.m4s from running ffmpeg (generating until {GeneratingUntil})",
                    job.JobId,
                    job.GeneratingUntilSegmentIndex);
                return;
            }

            if (job.FfmpegTask is { IsCompleted: true, IsFaulted: true })
            {
                var fault = job.FfmpegTask.Exception?.GetBaseException();
                logger.LogError(fault, "Job {JobId}: ffmpeg task faulted while ensuring init", job.JobId);
                job.FfmpegTask = null;
            }

            // Prefer an existing media window, else the advertised target (mid-resume), else 0.
            // Never "wait for media-driven ffmpeg": ExoPlayer fetches init.m4s before any
            // media segment, so that wait deadlocks demuxed HLS (client 8s timeout -> HTTP 499).
            var currentIndex = job.GetCurrentSegmentIndex();
            int startSegmentIndex;
            if (currentIndex >= 0)
            {
                startSegmentIndex = Math.Clamp(currentIndex - 5, 0, allSegments.Count - 1);
            }
            else if (job.TargetSegmentIndex > job.BufferSize)
            {
                startSegmentIndex = Math.Clamp(
                    job.TargetSegmentIndex - job.BufferSize,
                    0,
                    allSegments.Count - 1);
            }
            else
            {
                startSegmentIndex = 0;
            }

            logger.LogInformation(
                "Job {JobId}: init.m4s not ready; starting ffmpeg at segment {Start} (target={Target}, current={Current})",
                job.JobId,
                startSegmentIndex,
                job.TargetSegmentIndex,
                currentIndex);

            if (job.IsCopyRemux)
                await SpawnRemuxHeadAsync(job, startSegmentIndex, allSegments, cancellationToken);
            else
                await RestartJobWithSeekAsync(job, startSegmentIndex, allSegments, cancellationToken);
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
            TryDeleteJobCache(job);
        }

        await CleanupOrphanedTranscodeDirectoriesAsync(cancellationToken);
    }

    public int GetCurrentSegmentIndex(Guid jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            return job.GetCurrentSegmentIndex();
        }
        return -1;
    }

    public TranscodeJob? FindVideoJobForIndexedFile(Guid indexedFileId)
    {
        foreach (var job in _activeJobs.Values)
        {
            if (!job.IsAudioOnly && job.IndexedFileId == indexedFileId)
                return job;
        }

        return null;
    }

    private void TryDeleteJobCache(TranscodeJob job)
    {
        TryDeleteDirectory(job.OutputDirectory, recursive: true, job.JobId);

        var parentDir = Path.GetDirectoryName(job.OutputDirectory);
        if (parentDir is null)
            return;

        if (HasActiveJobsForIndexedFile(job.IndexedFileId))
            return;

        TryDeleteDirectory(
            Path.Combine(parentDir, Hls.SubtitlesCacheDirectoryName),
            recursive: true,
            job.JobId);
        TryDeleteDirectory(parentDir, recursive: false, job.JobId);
    }

    private bool HasActiveJobsForIndexedFile(Guid indexedFileId) =>
        _activeJobs.Values.Any(j => j.IndexedFileId == indexedFileId);

    private async Task CleanupOrphanedTranscodeDirectoriesAsync(CancellationToken cancellationToken)
    {
        var transcodingPath = _pathsConfig.Transcoding;
        if (string.IsNullOrEmpty(transcodingPath) || !Directory.Exists(transcodingPath))
            return;

        await _jobLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var fileDir in Directory.EnumerateDirectories(transcodingPath))
            {
                if (!Guid.TryParseExact(Path.GetFileName(fileDir), "N", out var indexedFileId))
                    continue;

                if (HasActiveJobsForIndexedFile(indexedFileId))
                    continue;

                TryDeleteDirectory(fileDir, recursive: true, jobId: null);
            }
        }
        finally
        {
            _jobLock.Release();
        }
    }

    private void TryDeleteDirectory(string directory, bool recursive, Guid? jobId)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, recursive);
            if (jobId.HasValue)
            {
                logger.LogInformation(
                    "Deleted transcode directory for job {JobId}: {Dir}",
                    jobId.Value,
                    directory);
            }
            else
            {
                logger.LogInformation("Deleted orphaned transcode directory {Dir}", directory);
            }
        }
        catch (IOException) when (!recursive)
        {
            // Parent still has other entries or raced with another job.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete transcode directory {Dir}", directory);
        }
    }

    private async Task RestartJobWithSeekAsync(
        TranscodeJob job,
        int startSegmentIndex,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken,
        bool purgeExisting = true)
    {
        await StopFfmpegAsync(job);

        if (purgeExisting)
            PurgeGeneratedSegments(job.OutputDirectory);

        job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
            startSegmentIndex,
            purgeExisting ? 0 : job.TargetSegmentIndex,
            job.BufferSize,
            allSegments.Count,
            job.IsCopyRemux);

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

    private void PurgeUnreadySegmentsInRange(string outputDirectory, int startSegmentIndex, int endSegmentIndexExclusive)
    {
        if (!Directory.Exists(outputDirectory))
            return;

        for (var i = startSegmentIndex; i < endSegmentIndexExclusive; i++)
        {
            var path = Path.Combine(outputDirectory, $"{i}.m4s");
            if (!File.Exists(path))
                continue;

            try
            {
                if (new FileInfo(path).Length >= 32)
                    continue;

                File.Delete(path);
                logger.LogDebug("Removed empty placeholder segment {Path}", path);
            }
            catch (IOException ex)
            {
                logger.LogDebug(ex, "Could not remove placeholder segment {Path}", path);
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

        // Mid-seek windows can leave unready placeholders; currentIndex is then -1.
        // Prefer the lowest ready media index, else resume near the client target
        // (never fall back to 0 while TargetSegmentIndex is mid-file).
        if (currentIndex < 0)
        {
            var lowestReady = FindLowestReadySegmentIndex(job.OutputDirectory);
            if (lowestReady >= 0)
            {
                startIndex = lowestReady;
            }
            else if (job.TargetSegmentIndex > 0)
            {
                startIndex = Math.Clamp(
                    job.TargetSegmentIndex - job.BufferSize,
                    0,
                    allSegments.Count - 1);
            }
        }

        if (startIndex >= allSegments.Count)
            return;

        if (job.IsCopyRemux)
        {
            job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveAdvertisedTarget(
                startIndex,
                job.TargetSegmentIndex,
                job.BufferSize,
                allSegments.Count,
                remuxToEnd: true);
            await SpawnRemuxHeadAsync(job, startIndex, allSegments, cancellationToken);
            return;
        }

        job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveContinueTarget(
            startIndex,
            job.TargetSegmentIndex,
            job.BufferSize,
            allSegments.Count);

        await StartFfmpegAsync(job, startIndex, allSegments, cancellationToken);
    }

    private static int FindLowestReadySegmentIndex(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
            return -1;

        var lowest = -1;
        foreach (var file in Directory.EnumerateFiles(outputDirectory, "*.m4s"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(name, "init", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!int.TryParse(name, out var index) || index < 0)
                continue;

            try
            {
                if (new FileInfo(file).Length < 32)
                    continue;
            }
            catch (IOException)
            {
                continue;
            }

            if (lowest < 0 || index < lowest)
                lowest = index;
        }

        return lowest;
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

        if (job.IsCopyRemux)
        {
            await SpawnRemuxHeadAsync(job, startSegmentIndex, allSegments, cancellationToken);
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

        // Determine video and audio codecs for transcoding
        var videoCodec = job.VideoCodec != "copy" ? job.VideoCodec : null;
        var audioCodec = job.AudioCodec != "copy" ? job.AudioCodec : null;
        var endSegmentIndex = Math.Min(job.TargetSegmentIndex + 1, allSegments.Count);

        // Drop empty placeholders from a previous failed window so GetCurrentSegmentIndex
        // and WaitUntilAvailable do not keep seeing stale unready files.
        PurgeUnreadySegmentsInRange(job.OutputDirectory, startSegmentIndex, endSegmentIndex);

        try
        {
            job.FfmpegCancellation?.Dispose();
            job.FfmpegCancellation = new CancellationTokenSource();
            var ffmpegToken = job.FfmpegCancellation.Token;
            job.GeneratingFromSegmentIndex = startSegmentIndex;
            job.GeneratingUntilSegmentIndex = endSegmentIndex - 1;

            // Start ffmpeg in background, owned only by the job lifetime
            var ffmpegTask = Task.Run(async () =>
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

            _ = ffmpegTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var fault = t.Exception?.GetBaseException();
                    if (fault is not null and not OperationCanceledException)
                    {
                        _ = PublishTranscodeFailedAsync(
                            job.IndexedFileId,
                            Path.GetFileName(job.InputFilePath),
                            fault.Message);
                    }

                    return;
                }

                if (t.IsCanceled)
                    return;

                // Close the window-boundary gap when Target already sits past ready segments
                // (client sliding window). Do not raise Target here - that stays request-driven.
                _ = TryContinueAfterFfmpegWindowAsync(job, t);
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

            job.FfmpegTask = ffmpegTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId}: Failed to start ffmpeg task", job.JobId);
            await PublishTranscodeFailedAsync(
                job.IndexedFileId,
                Path.GetFileName(job.InputFilePath),
                ex.Message);
            throw;
        }
    }

    private async Task TryContinueAfterFfmpegWindowAsync(TranscodeJob job, Task completedFfmpegTask)
    {
        if (!_activeJobs.ContainsKey(job.JobId))
            return;

        if (job.AttachedStreamSessions.IsEmpty)
            return;

        // Serialize with EnsureSegment / Restart so we never start a second ffmpeg.
        await job.FfmpegStartLock.WaitAsync();
        try
        {
            if (!_activeJobs.ContainsKey(job.JobId) || job.AttachedStreamSessions.IsEmpty)
                return;

            // A newer window already started, or StopFfmpeg cleared the task.
            if (!ReferenceEquals(job.FfmpegTask, completedFfmpegTask))
                return;

            var allSegments = await LoadStreamingSegmentsForJobAsync(job.IndexedFileId);
            if (allSegments.Count == 0)
                return;

            await ContinueTowardClientTargetIfNeededAsync(job, allSegments, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Job {JobId}: Failed to auto-continue after ffmpeg window", job.JobId);
        }
        finally
        {
            job.FfmpegStartLock.Release();
        }
    }

    /// <summary>
    /// Caller must hold <see cref="TranscodeJob.FfmpegStartLock"/>. Starts the next window only
    /// when the request-driven Target is past the highest ready segment.
    /// </summary>
    private async Task ContinueTowardClientTargetIfNeededAsync(
        TranscodeJob job,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        if (job.IsFfmpegRunning)
            return;

        if (job.FfmpegTask is { IsCompleted: true, IsFaulted: true })
        {
            var fault = job.FfmpegTask.Exception?.GetBaseException();
            logger.LogError(fault, "Job {JobId}: ffmpeg task faulted", job.JobId);
            job.FfmpegTask = null;
        }

        var currentIndex = job.GetCurrentSegmentIndex();
        if (!job.IsCopyRemux
            && FfmpegWindowAutoContinue.ShouldKeepLookahead(
                currentIndex,
                job.TargetSegmentIndex,
                job.LastRequestedSegmentIndex,
                job.BufferSize,
                allSegments.Count))
        {
            job.TargetSegmentIndex = FfmpegWindowAutoContinue.ResolveContinueTarget(
                currentIndex + 1,
                job.TargetSegmentIndex,
                job.BufferSize,
                allSegments.Count);
        }

        if (!FfmpegWindowAutoContinue.ShouldContinueTowardClientTarget(
                currentIndex,
                job.TargetSegmentIndex,
                allSegments.Count))
        {
            return;
        }

        logger.LogDebug(
            "Job {JobId}: Continuing toward client target (current={Current}, target={Target})",
            job.JobId,
            currentIndex,
            job.TargetSegmentIndex);

        await ContinueJobAsync(job, allSegments, cancellationToken);
    }

    private async Task<List<HlsSegment>> LoadStreamingSegmentsForJobAsync(Guid indexedFileId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var keyframeSegments = await HlsSegmentHelper.LoadSegmentsAsync(db, indexedFileId);
        long totalDurationMs;
        if (keyframeSegments.Count > 0)
        {
            totalDurationMs = keyframeSegments.Sum(s => s.Duration);
        }
        else
        {
            var entity = await db.IndexedFiles
                .AsNoTracking()
                .Include(f => f.FileMetadata)
                .FirstOrDefaultAsync(f => f.Id == indexedFileId);
            totalDurationMs = entity?.FileMetadata switch
            {
                VideoFileMetadata video => (long)video.Duration.TotalMilliseconds,
                AudioFileMetadata audio => (long)audio.Duration.TotalMilliseconds,
                _ => 0
            };
            if (totalDurationMs <= 0)
                return [];
        }

        return HlsSegmentHelper.ResolveStreamingSegments(keyframeSegments, totalDurationMs);
    }

    private async Task PublishTranscodeFailedAsync(Guid indexedFileId, string? mediaTitle, string errorMessage)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            await publisher.PublishAsync(new TranscodeFailedEvent(indexedFileId, mediaTitle, errorMessage));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish TranscodeFailedEvent for IndexedFile {IndexedFileId}", indexedFileId);
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
        var remuxHeads = job.RemuxHeads.Values.ToList();
        foreach (var head in remuxHeads)
        {
            try
            {
                if (!head.Cancellation.IsCancellationRequested)
                    await head.Cancellation.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        foreach (var head in remuxHeads)
        {
            if (head.Task is null)
                continue;

            try
            {
                await head.Task;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Remux head ended with error for job {JobId}", job.JobId);
            }
        }

        if (job.FfmpegCancellation is null && remuxHeads.Count == 0)
            return;

        try
        {
            if (job.FfmpegCancellation is not null && !job.FfmpegCancellation.IsCancellationRequested)
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
            job.FfmpegCancellation?.Dispose();
            job.FfmpegCancellation = null;
            job.FfmpegTask = null;
            job.GeneratingUntilSegmentIndex = -1;
            job.RemuxHeads.Clear();
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
