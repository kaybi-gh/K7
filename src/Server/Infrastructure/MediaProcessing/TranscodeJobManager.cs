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
        int? subtitleBurnInStreamIndex = null,
        int? audioChannels = null)
    {
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        EnsureTempQuotaAvailable(settings.TranscodeTempQuotaMb);

        // Channel-capped encodes are distinct outputs (a stereo browser and a 5.1 TV must
        // not share aac segments); fold the count into the codec id for key and directory.
        var audioCodecId = audioCodec is null
            ? "copy"
            : audioChannels is > 0
                ? $"{audioCodec}{audioChannels.Value.ToString(CultureInfo.InvariantCulture)}ch"
                : audioCodec;
        var jobKey = GenerateJobKey(indexedFileId, quality, videoCodec ?? "copy", audioCodecId, audioTrackIndex, isAudioOnly, subtitleBurnInStreamIndex);

        if (_activeJobs.TryGetValue(jobKey, out var existingJob))
        {
            existingJob.AttachedStreamSessions.TryAdd(streamSessionId, 0);
            existingJob.LastPingTime = DateTime.UtcNow;
            await RecoverWipedOutputIfNeededAsync(existingJob, cancellationToken);
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
                await RecoverWipedOutputIfNeededAsync(existingJob, cancellationToken);
                return existingJob;
            }

            // Create new job - note: we don't start ffmpeg here yet
            // It will be started on-demand in EnsureSegmentWillBeGeneratedAsync
            var transcodingPath = _pathsConfig.Transcoding ?? throw new InvalidOperationException("Transcoding path not configured");
            var videoSubDir = subtitleBurnInStreamIndex.HasValue
                ? $"video-{quality}-{videoCodec ?? "copy"}-sub{subtitleBurnInStreamIndex.Value}"
                : $"video-{quality}-{videoCodec ?? "copy"}";

            var outputDir = FfmpegStreamingArgs.NormalizeOutputDirectory(isAudioOnly
                ? Path.Combine(transcodingPath, indexedFileId.ToString("N"), $"audio-{audioCodecId}-a{audioTrackIndex}")
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
                AudioChannels = audioCodec is null ? null : audioChannels,
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

        await RecoverWipedOutputIfNeededAsync(job, cancellationToken);

        // init.m4s: never map to media segment 0 (that false-triggers seek-to-start on resume).
        if (requestedSegmentIndex < 0)
        {
            await EnsureInitWillBeGeneratedAsync(job, allSegments, cancellationToken);
            return;
        }

        var previousClientRequest = job.LastClientMediaSegmentRequest;
        job.LastClientMediaSegmentRequest = requestedSegmentIndex;
        job.LastRequestedSegmentIndex = Math.Max(job.LastRequestedSegmentIndex, requestedSegmentIndex);

        // Open-GOP remux: CRA is demoted on serve so linear play does not flush every GOP.
        // A seek landing must keep the sync flag even when the .m4s already exists (no new
        // head). Otherwise ExoPlayer has audio at T and no video RAP, so the last frame freezes.
        if (!job.IsAudioOnly
            && job.IsCopyRemux
            && FfmpegRemuxSeekPolicy.IsClientSeekJump(previousClientRequest, requestedSegmentIndex))
        {
            job.RemuxRapSegmentIndices[requestedSegmentIndex] = 0;
        }

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
            // Covered only up to the live TIP (segment produced), not the head's EOF target.
            // Using UntilInclusive made a far forward seek look covered by the 0-head, so no
            // new head spawned and the client waited on the slow linear catch-up.
            var covered = liveHeads.Any(h =>
                h.Running && requestedSegmentIndex >= h.From && requestedSegmentIndex <= h.TipIndex);
            var distance = FfmpegRemuxSeekPolicy.MinDistanceSecondsToLiveHead(
                requestedSegmentIndex,
                liveHeads.Select(h => (h.From, h.TipIndex, h.UntilInclusive, h.Running)),
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
                // Far forward seek: re-anchor the window but keep already-encoded segments so a
                // later seek back into them serves instantly instead of re-encoding.
                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(
                    job,
                    startSegmentIndex,
                    allSegments,
                    cancellationToken,
                    purgeExisting: false);
            }
            else if (gap < 0)
            {
                // Seek back before the current window: re-anchor, keep existing segments.
                var startSegmentIndex = Math.Clamp(requestedSegmentIndex - 5, 0, allSegments.Count - 1);
                await RestartJobWithSeekAsync(
                    job,
                    startSegmentIndex,
                    allSegments,
                    cancellationToken,
                    purgeExisting: false);
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

        // Remux copy heads are bitstream copies (I/O bound, no encoder): they never wait
        // for an encode slot. The caller holds FfmpegStartLock here, so waiting would also
        // block every segment request of this job while other jobs occupy the slots.
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
                        audioCodec,
                        job.AudioChannels);
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
                // A head stopped early (killed mid-file) may leave a truncated last file:
                // keep the closed-only rule for it. A natural exit closed every file.
                var stoppedEarly = cts.IsCancellationRequested;
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
                PromoteRemuxHeadOnce(job, head, ffmpegExited: !stoppedEarly);
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
            PromoteRemuxHeadOnce(job, head, ffmpegExited: false);

            // Stop this head when the next shared segment is already ready, but only once
            // it has delivered its own landing segment. TipIndex starts at From before
            // anything is written: with From missing and From+1 ready (hole left by an
            // early-stopped head) the check fired immediately, the waiter re-kicked a new
            // head, and the job spawned dozens of heads per second without ever filling From.
            var next = head.TipIndex + 1;
            if (next <= head.UntilInclusive
                && next > head.From
                && HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, head.From)
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

    private void PromoteRemuxHeadOnce(TranscodeJob job, TranscodeRemuxHead head, bool ffmpegExited)
    {
        // Promote closed media first so shared init can satisfy sibling-ready checks.
        // "Closed" means ffmpeg moved on to the next file (or exited). A file whose boxes
        // walk as complete can still be mid-write: frag_keyframe flushes a fragment at each
        // collapsed interior keyframe, and copying that snapshot froze truncated segments
        // (video holes) into the immutable shared cache.
        for (var i = head.From; i <= head.UntilInclusive; i++)
        {
            var stagingPath = Path.Combine(
                head.StagingDirectory,
                i.ToString(CultureInfo.InvariantCulture) + ".m4s");
            if (!File.Exists(stagingPath))
                continue;

            if (!RemuxSegmentPromoter.IsStagingSegmentClosed(head.StagingDirectory, i, ffmpegExited))
                break;

            if (!HlsSegmentFileWaiter.IsSegmentFileReady(stagingPath))
                continue;

            var shared = false;
            if (RemuxSegmentPromoter.TryPromoteMediaSegment(head.StagingDirectory, job.OutputDirectory, i))
            {
                shared = true;
                job.RemuxSegmentOwners.TryAdd(i, head.Id);
                if (i >= head.TipIndex)
                    head.TipIndex = i;
            }
            else if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, i))
            {
                shared = true;
                if (i >= head.TipIndex)
                {
                    job.RemuxSegmentOwners.TryAdd(i, head.Id);
                    head.TipIndex = i;
                }
            }

            // Drop the closed staging copy once the shared cache has it. Every 50ms pass
            // re-read and re-walked each staging file otherwise (whole head history),
            // which starved promotion of new segments on long remuxes.
            if (shared)
                RemuxSegmentPromoter.TryDeleteStagingSegment(head.StagingDirectory, i);
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

            // Active remux head or encode ffmpeg will write init.m4s; do not restart from segment 0
            // unless the cache was wiped under that process (Recover already stopped it).
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

            // Prefer an existing media window, else the last client media GET (resume), else 0.
            // Never "wait for media-driven ffmpeg": ExoPlayer fetches init.m4s before any
            // media segment, so that wait deadlocks demuxed HLS (client 8s timeout -> HTTP 499).
            // Never use a stale Target near EOF when the disk is empty: that remuxes from
            // the end and the client waits tens of seconds for init (then Video.js error 4).
            var currentIndex = job.GetCurrentSegmentIndex();
            var startSegmentIndex = TranscodeInitStartPolicy.ResolveStartIndex(
                currentIndex,
                job.LastClientMediaSegmentRequest,
                allSegments.Count);

            logger.LogInformation(
                "Job {JobId}: init.m4s not ready; starting ffmpeg at segment {Start} (target={Target}, current={Current}, lastClient={LastClient})",
                job.JobId,
                startSegmentIndex,
                job.TargetSegmentIndex,
                currentIndex,
                job.LastClientMediaSegmentRequest);

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

    /// <summary>
    /// Manual delete of the transcode cache leaves the in-memory job alive. ffmpeg may
    /// still be "running" against missing files, and Target sits near EOF from the last
    /// seek. Reset so the next init GET starts at 0 (or the resume landing) instead of
    /// remuxing the last 10 segments for 70s.
    /// </summary>
    private async Task RecoverWipedOutputIfNeededAsync(TranscodeJob job, CancellationToken cancellationToken)
    {
        if (!IsOutputCacheEmpty(job))
        {
            job.HasObservedReadyOutput = true;
            return;
        }

        if (!NeedsWipedOutputReset(job))
            return;

        await job.FfmpegStartLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsOutputCacheEmpty(job))
            {
                job.HasObservedReadyOutput = true;
                return;
            }

            if (!NeedsWipedOutputReset(job))
                return;

            logger.LogWarning(
                "Job {JobId}: output cache empty under a live job (target={Target}, lastClient={LastClient}) - reset generation",
                job.JobId,
                job.TargetSegmentIndex,
                job.LastClientMediaSegmentRequest);

            await StopFfmpegAsync(job);
            job.WindowStartIndex = -1;
            job.GeneratingFromSegmentIndex = -1;
            job.GeneratingUntilSegmentIndex = -1;
            job.RemuxSegmentOwners.Clear();
            job.RemuxRapSegmentIndices.Clear();
            // Forget landings from the wiped generation so init starts at 0, not a stale EOF.
            job.LastClientMediaSegmentRequest = -1;
            job.LastRequestedSegmentIndex = -1;
            job.TargetSegmentIndex = 0;
            job.HasObservedReadyOutput = false;
            Directory.CreateDirectory(job.OutputDirectory);
        }
        finally
        {
            job.FfmpegStartLock.Release();
        }
    }

    // Remux copy advertises Target = EOF and writes to head-* staging before promoting:
    // an empty shared dir with a live head is a cold start, not a wipe. The old rule
    // (Target > BufferSize) killed the resume head on the very next request and the
    // browser waited forever on init.m4s / segment 1055. Same for an encode resume
    // window that has not landed its first .m4s yet.
    private static bool NeedsWipedOutputReset(TranscodeJob job) =>
        TranscodeWipedOutputPolicy.NeedsReset(
            Directory.Exists(job.OutputDirectory),
            job.IsFfmpegRunning,
            job.IsCopyRemux,
            HasRemuxStaging(job),
            job.HasObservedReadyOutput,
            job.LastRequestedSegmentIndex,
            job.TargetSegmentIndex,
            job.WindowStartIndex,
            job.GeneratingFromSegmentIndex,
            job.BufferSize);

    private static bool HasRemuxStaging(TranscodeJob job)
    {
        if (job.RemuxHeads.Values.Any(static head =>
                !string.IsNullOrEmpty(head.StagingDirectory)
                && Directory.Exists(head.StagingDirectory)))
        {
            return true;
        }

        if (!Directory.Exists(job.OutputDirectory))
            return false;

        try
        {
            return Directory.EnumerateDirectories(job.OutputDirectory, "head-*").Any();
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool IsOutputCacheEmpty(TranscodeJob job)
    {
        if (HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory))
            return false;

        if (!Directory.Exists(job.OutputDirectory))
            return true;

        try
        {
            foreach (var file in Directory.EnumerateFiles(job.OutputDirectory, "*.m4s"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(name, "init", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (HlsSegmentFileWaiter.IsSegmentFileReady(file))
                    return false;
            }
        }
        catch (IOException)
        {
            return true;
        }

        return true;
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

    public async Task ReleaseSessionAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        foreach (var job in _activeJobs.Values.ToList())
        {
            if (!job.AttachedStreamSessions.TryRemove(streamSessionId, out _))
                continue;

            if (!job.AttachedStreamSessions.IsEmpty || !job.IsFfmpegRunning)
                continue;

            // Nobody is watching: stop ffmpeg now instead of letting the window / remux head
            // run to its target. A relaunch otherwise paid the stop of that process (up to
            // ~10s on the AAC window) on its first segment request, and the running process
            // held a transcode slot. Ready segments are kept for reuse.
            await job.FfmpegStartLock.WaitAsync(cancellationToken);
            try
            {
                if (!job.AttachedStreamSessions.IsEmpty)
                    continue;

                logger.LogInformation(
                    "Job {JobId}: last session {SessionId} closed, stopping ffmpeg (cache kept)",
                    job.JobId,
                    streamSessionId);
                await StopFfmpegAsync(job);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Job {JobId}: failed to stop ffmpeg on session release", job.JobId);
            }
            finally
            {
                job.FfmpegStartLock.Release();
            }
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

        // Re-anchor the contiguous-ready scan at this window start. Preserved across
        // cooperative continues (which call StartFfmpegAsync directly, not this method), so a
        // no-purge seek that keeps far-away segments cannot fool GetCurrentSegmentIndex.
        job.WindowStartIndex = startSegmentIndex;

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

        // Anchor the first encode window here. Seeks re-anchor via RestartJobWithSeekAsync;
        // cooperative continues keep the original anchor so the whole window stays contiguous.
        if (job.WindowStartIndex < 0)
            job.WindowStartIndex = startSegmentIndex;

        var segmentsToGenerate = job.TargetSegmentIndex - startSegmentIndex + 1;
        if (segmentsToGenerate <= 0)
        {
            return;
        }

        // Slot wait may use the request token, but ffmpeg itself must NOT - otherwise a
        // client abort/timeout on one segment request kills generation for everyone.
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        await WaitForTranscodeSlotAsync(job, settings.MaxConcurrentTranscodes, cancellationToken);

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
                        audioCodec,
                        job.AudioChannels);
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

        // Bounded waits: callers hold FfmpegStartLock. If ffmpeg ignores the cancel for a
        // while, every segment request of this job would otherwise hang on the lock.
        foreach (var head in remuxHeads)
        {
            if (head.Task is null)
                continue;

            await AwaitFfmpegExitBoundedAsync(head.Task, job, "remux head " + head.Id.ToString(CultureInfo.InvariantCulture));
        }

        if (job.FfmpegCancellation is null && remuxHeads.Count == 0)
            return;

        try
        {
            if (job.FfmpegCancellation is not null && !job.FfmpegCancellation.IsCancellationRequested)
                job.FfmpegCancellation.Cancel();

            if (job.FfmpegTask is not null)
                await AwaitFfmpegExitBoundedAsync(job.FfmpegTask, job, "ffmpeg task");
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

    // The requesting job never counts against its own slot: a remux job spawning a second
    // head (seek back, restart from 0) while its first head runs to EOF used to wait on
    // itself until the head finished, and the session create that awaited the prefetch hung.
    private async Task WaitForTranscodeSlotAsync(TranscodeJob job, int maxConcurrent, CancellationToken cancellationToken)
    {
        if (maxConcurrent <= 0)
            return;

        // Bounded: the callers hold the job's FfmpegStartLock, so an unbounded wait here
        // stalls every segment request of the job (init.m4s included) for as long as other
        // jobs keep their slots. Past the bound, start anyway and let the OS share the CPU.
        var deadline = DateTime.UtcNow + TranscodeSlotMaxWait;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var running = _activeJobs.Values.Count(j => !ReferenceEquals(j, job) && j.IsFfmpegRunning);
            if (running < maxConcurrent)
                return;

            if (DateTime.UtcNow >= deadline)
            {
                logger.LogWarning(
                    "Job {JobId}: {Running} transcodes running (max {Max}); slot wait exceeded {Seconds}s, starting anyway",
                    job.JobId,
                    running,
                    maxConcurrent,
                    TranscodeSlotMaxWait.TotalSeconds);
                return;
            }

            await Task.Delay(500, cancellationToken);
        }
    }

    private static readonly TimeSpan TranscodeSlotMaxWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FfmpegStopMaxWait = TimeSpan.FromSeconds(10);

    private async Task AwaitFfmpegExitBoundedAsync(Task task, TranscodeJob job, string what)
    {
        try
        {
            var finished = await Task.WhenAny(task, Task.Delay(FfmpegStopMaxWait));
            if (finished != task)
            {
                logger.LogWarning(
                    "Job {JobId}: {What} did not exit within {Seconds}s after cancel; continuing without it",
                    job.JobId,
                    what,
                    FfmpegStopMaxWait.TotalSeconds);
                return;
            }

            await task;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "{What} ended with error for job {JobId}", what, job.JobId);
        }
    }
}
