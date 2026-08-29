using K7.Server.Application.Common;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetEffectiveTrackSelectionPreferences;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class StreamPlaybackService(
    IApplicationDbContext context,
    IMediaAccessGuard accessGuard,
    ISender sender,
    IActiveStreamTracker activeStreamTracker,
    ITranscodeJobManager transcodeJobManager,
    IFfmpegCapabilitiesService ffmpegCapabilitiesService,
    IPlaybackBoostService playbackBoostService,
    ILogger<StreamPlaybackService> logger) : IStreamPlaybackService
{
    public async Task<IndexedFileStreamUri> GetStreamUriAsync(
        GetStreamUriQuery query,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(query.DeviceId);
        await accessGuard.EnsureAccessByIndexedFileAsync(query.Id, cancellationToken);

        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);
        Guard.Against.NotFound(query.Id, indexedFile);

        var device = await context.Devices.FindAsync([query.DeviceId], cancellationToken);
        Guard.Against.NotFound((Guid)query.DeviceId, device);

        if (indexedFile.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            await context.Entry(audioFileMetadata).Reference(a => a.AudioTrack).LoadAsync(cancellationToken);
            var (uri, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(device, indexedFile, audioFileMetadata, query);
            activeStreamTracker.UpdateStreamDecision(query.StreamSessionId, decision);
            return uri;
        }

        if (indexedFile.FileMetadata is null)
        {
            // Indexed but not probed yet: an expected transient state, not a server fault. Raise the
            // priority of the pending probe so that what a user asked for overtakes the backlog.
            await playbackBoostService.BoostPendingWorkAsync(indexedFile.Id, indexedFile.MediaId, cancellationToken);
            throw new MediaNotReadyException(indexedFile.Id);
        }

        if (indexedFile.FileMetadata is not VideoFileMetadata videoFileMetadata)
        {
            throw new InvalidOperationException(
                $"Indexed file '{indexedFile.Id}' has unsupported metadata type '{indexedFile.FileMetadata.GetType().Name}'.");
        }

        await context.Entry(videoFileMetadata).Collection(v => v.AudioTracks).LoadAsync(cancellationToken);
        await context.Entry(videoFileMetadata).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
        await context.Entry(videoFileMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

        var hlsSegmentsAvailable = await HlsSegmentHelper.HasSegmentsAsync(context, query.Id, cancellationToken);
        if (!hlsSegmentsAvailable)
        {
            await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(sender, query.Id, logger, cancellationToken);
        }

        if (ChapterExtractionHelper.NeedsExtraction(videoFileMetadata))
        {
            await ChapterExtractionHelper.EnsureChaptersAsync(context, sender, query.Id, logger, cancellationToken);
            await context.Entry(videoFileMetadata).ReloadAsync(cancellationToken);
        }

        var subtitleTrackIndex = query.SubtitleTrackIndex;
        if (query.AudioTrackIndex is null)
        {
            var preferences = await sender.Send(
                new GetEffectiveTrackSelectionPreferencesQuery { LibraryId = indexedFile.LibraryId },
                cancellationToken);
            var audioDtos = videoFileMetadata.AudioTracks.OrderBy(t => t.Index).Select(t => t.ToAudioFileTrackDto()).ToList();
            var subtitleDtos = videoFileMetadata.SubtitleTracks.OrderBy(t => t.Index).Select(t => t.ToSubtitleFileTrackDto()).ToList();
            var selection = TrackSelector.SelectTracks(preferences, audioDtos, subtitleDtos);
            query.AudioTrackIndex = selection.AudioTrackIndex;
            subtitleTrackIndex ??= selection.SubtitleTrackIndex;
            query.SubtitleTrackIndex = subtitleTrackIndex;
        }

        var (streamUri, streamDecision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, videoFileMetadata, query, hlsSegmentsAvailable, subtitleTrackIndex);
        streamDecision = await StreamDecisionEnrichment.EnrichEncodersAsync(
            streamDecision, ffmpegCapabilitiesService, cancellationToken);
        activeStreamTracker.UpdateStreamDecision(query.StreamSessionId, streamDecision);
        return streamUri;
    }

    public async Task<HttpContentResult> GetHlsVideoSegmentAsync(
        GetHlsVideoStreamSegmentQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Quality != "original")
        {
            var qualityDef = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
            Guard.Against.Null(qualityDef, nameof(query.Quality), $"Provided quality '{query.Quality}' is not valid.");
        }

        var entity = await context.IndexedFiles.Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);
        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        if (!new FileInfo(entity.Path).Exists)
            return new EmptyHttpContentResult(404);

        var isTransmuxing = query.Quality == "original"
            && string.IsNullOrEmpty(query.TranscodingVideoCodec)
            && !query.SubtitleBurnInStreamIndex.HasValue;
        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(context, query.Id, cancellationToken);
        var effectiveTranscodingVideoCodec = query.TranscodingVideoCodec;

        if (isTransmuxing && hlsSegments.Count == 0)
        {
            await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(sender, query.Id, logger, cancellationToken);
            isTransmuxing = false;
            effectiveTranscodingVideoCodec ??= HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        List<HlsSegment> allSegments;
        var totalDurationMs = hlsSegments.Count > 0
            ? hlsSegments.Sum(s => s.Duration)
            : entity.FileMetadata is VideoFileMetadata videoMetadata
                ? (long)videoMetadata.Duration.TotalMilliseconds
                : throw new InvalidOperationException("Cannot determine duration for HLS video segment");

        // Same keyframe timeline for transmux and transcode so ABR quality switches stay seamless.
        // Never truncate for init.m4s: EnsureInit shares this list with ContinueJobAsync, and a
        // short list races mid-seek media requests (wrong totalSegments / stuck generation).
        allSegments = HlsSegmentHelper.ResolveVideoStreamingSegments(hlsSegments, totalDurationMs);

        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
            return new EmptyHttpContentResult(404);

        var videoCodec = effectiveTranscodingVideoCodec
            ?? (query.SubtitleBurnInStreamIndex.HasValue ? "h264" : null);
        videoCodec = await ApplyVideoStreamDecisionAsync(entity, query, videoCodec, cancellationToken);
        await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(
            query.StreamSessionId, activeStreamTracker, ffmpegCapabilitiesService, cancellationToken);

        var job = await transcodeJobManager.GetOrStartJobAsync(
            query.Id, entity.Path, query.Quality, videoCodec, audioCodec: null, audioTrackIndex: 0,
            isAudioOnly: false, query.StreamSessionId, cancellationToken, query.SubtitleBurnInStreamIndex);
        transcodeJobManager.PingJob(job.JobId, query.StreamSessionId);

        var segmentPath = Path.Combine(job.OutputDirectory, query.SegmentNumber == -1 ? "init.m4s" : $"{query.SegmentNumber}.m4s");
        // Keep -1 for init.m4s so EnsureSegmentWillBeGeneratedAsync does not treat it as a seek to media segment 0.
        return await GetSegmentResultAsync(
            segmentPath, job, query.SegmentNumber, allSegments, query.SegmentNumber, "video/mp4", cancellationToken);
    }

    public async Task<HttpContentResult> GetHlsAudioSegmentAsync(
        GetHlsAudioStreamSegmentQuery query,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.IndexedFiles.Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);
        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        if (!new FileInfo(entity.Path).Exists)
            return new EmptyHttpContentResult(404);

        // Must load DB keyframe rows (same as video). Navigation on FileMetadata is often
        // unloaded here and would silently fall back to equal-length -> A/V desync.
        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(context, query.Id, cancellationToken);
        var totalDurationMs = hlsSegments.Count > 0
            ? hlsSegments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata videoMetadata => (long)videoMetadata.Duration.TotalMilliseconds,
                AudioFileMetadata audioMetadata => (long)audioMetadata.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS audio segment")
            };
        var allSegments = HlsSegmentHelper.ResolveStreamingSegments(hlsSegments, totalDurationMs);
        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
            return new EmptyHttpContentResult(404);

        var job = await transcodeJobManager.GetOrStartJobAsync(
            query.Id, entity.Path, quality: "original", videoCodec: null, audioCodec: query.TranscodingAudioCodec,
            audioTrackIndex: query.AudioTrackIndex, isAudioOnly: true, query.StreamSessionId, cancellationToken);
        transcodeJobManager.PingJob(job.JobId, query.StreamSessionId);

        var segmentPath = Path.Combine(job.OutputDirectory, query.SegmentNumber == -1 ? "init.m4s" : $"{query.SegmentNumber}.m4s");
        // Keep -1 for init.m4s so EnsureSegmentWillBeGeneratedAsync does not treat it as a seek to media segment 0.
        return await GetSegmentResultAsync(
            segmentPath, job, query.SegmentNumber, allSegments, query.SegmentNumber, "audio/mp4", cancellationToken);
    }

    private async Task<string?> ApplyVideoStreamDecisionAsync(
        IndexedFile entity,
        GetHlsVideoStreamSegmentQuery query,
        string? videoCodec,
        CancellationToken cancellationToken)
    {
        if (query.Quality != "original" && entity.FileMetadata is VideoFileMetadata videoMetadataForQuality)
        {
            var requestedQuality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
            if (requestedQuality.Value is not null)
            {
                var fileResolution = Constants.VideoQualities.Single(x => x.Key == videoMetadataForQuality.VideoResolution).Value;
                if (requestedQuality.Value.Height <= fileResolution.Height)
                {
                    await context.Entry(videoMetadataForQuality).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
                    var videoTrack = videoMetadataForQuality.VideoTracks.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Index).FirstOrDefault();
                    var sourceResolution = videoTrack is { Width: > 0, Height: > 0 }
                        ? $"{videoTrack.Width}x{videoTrack.Height}"
                        : $"{fileResolution.Width}x{fileResolution.Height}";
                    videoCodec ??= "h264";
                    var existing = activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
                    activeStreamTracker.UpdateStreamDecision(
                        query.StreamSessionId,
                        StreamDecisionExtensions.ApplyQualityDownscale(existing, requestedQuality.Value, videoCodec, sourceResolution));
                }
            }
        }

        if (query.SubtitleBurnInStreamIndex is not int burnInIndex || entity.FileMetadata is not VideoFileMetadata videoMetadata)
            return videoCodec;

        await context.Entry(videoMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);
        var burnInTrack = videoMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == burnInIndex);
        if (burnInTrack is null)
        {
            logger.LogWarning(
                "Subtitle burn-in stream index {StreamIndex} not found among subtitle tracks for IndexedFile {Id}",
                burnInIndex, query.Id);
            return videoCodec;
        }

        var existingDecision = activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
        activeStreamTracker.UpdateStreamDecision(
            query.StreamSessionId,
            StreamDecisionExtensions.ApplySubtitleBurnIn(existingDecision, burnInTrack));
        return videoCodec;
    }

    private async Task<HttpContentResult> GetSegmentResultAsync(
        string segmentPath,
        TranscodeJob job,
        int requestedIndex,
        List<HlsSegment> allSegments,
        int segmentNumber,
        string contentType,
        CancellationToken cancellationToken)
    {
        // Demuxed HLS: if the paired video job already exists but init.m4s is not ready yet,
        // hold audio responses so ExoPlayer does not start parsing audio while video fMP4 is
        // still generating (PGS burn-in can take seconds). Wait for init only - mid-seek
        // windows never produce 0.m4s. Soft-gate only - never wait for a video job that has
        // not been created yet (avoids deadlock if audio is first).
        if (job.IsAudioOnly)
        {
            await WaitForExistingPairedVideoInitAsync(job, cancellationToken);
            if (segmentNumber >= 0)
                await WaitForPairedVideoSegmentAsync(job, segmentNumber, allSegments, cancellationToken);
        }

        var generationFailure = await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            segmentPath,
            job,
            ct => transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(job.JobId, requestedIndex, allSegments, ct),
            cancellationToken,
            maxTotalSeconds: segmentNumber == -1 ? 90 : 180);
        if (generationFailure is not null)
        {
            logger.LogError(
                generationFailure,
                "Segment {SegmentNumber} was not generated for job {JobId} (ffmpeg running: {FfmpegRunning}, unreadiness: {Unreadiness})",
                segmentNumber,
                job.JobId,
                job.FfmpegTask is { IsCompleted: false },
                HlsSegmentFileWaiter.DescribeUnreadiness(segmentPath));
            return new TextHttpContentResult(
                $"Transcoding failed: {generationFailure.Message}",
                "text/plain",
                503);
        }

        await HlsSegmentFileWaiter.WaitUntilReadableAsync(segmentPath, cancellationToken);
        if (!HlsSegmentFileWaiter.TryReadReadySegmentBytes(segmentPath, out var segmentBytes))
        {
            logger.LogError(
                "Segment {SegmentNumber} for job {JobId} was not a complete fMP4 after wait (path: {SegmentPath}, unreadiness: {Unreadiness})",
                segmentNumber,
                job.JobId,
                segmentPath,
                HlsSegmentFileWaiter.DescribeUnreadiness(segmentPath));
            return new TextHttpContentResult(
                "Transcoding failed: segment file is incomplete or corrupt.",
                "text/plain",
                503);
        }

        // Video copy: rebase only a true window reset (>= 1s). Do not flatten the
        // ~83ms source CTS onto the playlist. Video encode: tight align + subtract
        // first-sample CTS so hardware-encoder delay is not left as late video on ExoPlayer.
        if (segmentNumber >= 0 && segmentNumber < allSegments.Count)
        {
            var initPath = Path.Combine(
                Path.GetDirectoryName(segmentPath) ?? job.OutputDirectory,
                HlsSegmentFileWaiter.InitSegmentFileName);
            var startMs = allSegments[segmentNumber].StartTimestamp;
            var rebaseToleranceMs = job.IsAudioOnly
                ? Hls.TfdtWindowResetThresholdMs
                : Hls.VideoTfdtRebaseToleranceMs(IsVideoEncodeJob(job));
            if (Fmp4TfdtRebase.TryRebaseMediaSegment(
                    segmentBytes,
                    initPath,
                    startMs,
                    out var rebasedBytes,
                    out var rebaseDetail,
                    rebaseToleranceMs,
                    alignPresentationTime: !job.IsAudioOnly && IsVideoEncodeJob(job)))
            {
                segmentBytes = rebasedBytes;
                logger.LogDebug(
                    "Rebased fMP4 tfdt for segment {SegmentNumber} job {JobId}: {Detail}",
                    segmentNumber,
                    job.JobId,
                    rebaseDetail);
                var ffmpegRunning = job.FfmpegTask is { IsCompleted: false };
                if (!ffmpegRunning)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(segmentPath, segmentBytes, cancellationToken);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            else if (!rebaseDetail.StartsWith("already-absolute", StringComparison.Ordinal)
                     && rebaseDetail != "skipped")
            {
                logger.LogDebug(
                    "Skipped fMP4 tfdt rebase for segment {SegmentNumber} job {JobId}: {Detail}",
                    segmentNumber,
                    job.JobId,
                    rebaseDetail);
            }

            if (!job.IsAudioOnly
                && !IsVideoEncodeJob(job)
                && segmentNumber != job.GeneratingFromSegmentIndex
                && Fmp4OpenGopSync.TryDemoteCraFirstSample(
                    segmentBytes,
                    out var demotedBytes,
                    out var demoteDetail))
            {
                segmentBytes = demotedBytes;
                logger.LogDebug(
                    "Demoted open-GOP CRA sync flag for segment {SegmentNumber} job {JobId}: {Detail}",
                    segmentNumber,
                    job.JobId,
                    demoteDetail);
                var ffmpegRunning = job.FfmpegTask is { IsCompleted: false };
                if (!ffmpegRunning)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(segmentPath, segmentBytes, cancellationToken);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        // Serve a snapshot - never stream a live ffmpeg output file (partial init.m4s
        // parses as ISO BMFF garbage; ExoPlayer reports "Top bit not zero").
        return new BytesHttpContentResult(segmentBytes, contentType);
    }

    private async Task WaitForExistingPairedVideoInitAsync(
        TranscodeJob audioJob,
        CancellationToken cancellationToken)
    {
        var videoJob = transcodeJobManager.FindVideoJobForIndexedFile(audioJob.IndexedFileId);
        if (videoJob is null)
            return;

        // Hold until video init is ready so ExoPlayer does not start the audio MediaPeriod
        // while video fMP4 headers are still mid-generate (PGS burn-in). Mid-seek windows
        // produce init without ever writing 0.m4s.
        if (HlsSegmentFileWaiter.IsInitReadyOnDisk(videoJob.OutputDirectory))
            return;

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HlsSegmentFileWaiter.IsInitReadyOnDisk(videoJob.OutputDirectory))
                return;

            if (videoJob.FfmpegTask is { IsFaulted: true })
            {
                logger.LogWarning(
                    "Paired video job {VideoJobId} faulted while holding audio; serving audio without video gate",
                    videoJob.JobId);
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        logger.LogWarning(
            "Timed out waiting for paired video init ({VideoDir}); serving audio anyway",
            videoJob.OutputDirectory);
    }

    private async Task WaitForPairedVideoSegmentAsync(
        TranscodeJob audioJob,
        int segmentNumber,
        List<HlsSegment> allSegments,
        CancellationToken cancellationToken)
    {
        var videoJob = transcodeJobManager.FindVideoJobForIndexedFile(audioJob.IndexedFileId);
        if (videoJob is null)
            return;

        if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(videoJob.OutputDirectory, segmentNumber))
            return;

        await transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
            videoJob.JobId,
            segmentNumber,
            allSegments,
            cancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(videoJob.OutputDirectory, segmentNumber))
                return;

            if (videoJob.FfmpegTask is { IsFaulted: true })
            {
                logger.LogWarning(
                    "Paired video job {VideoJobId} faulted while holding audio segment {SegmentNumber}",
                    videoJob.JobId,
                    segmentNumber);
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        logger.LogWarning(
            "Timed out waiting for paired video segment {SegmentNumber} ({VideoDir}); serving audio anyway",
            segmentNumber,
            videoJob.OutputDirectory);
    }

    private static bool IsVideoEncodeJob(TranscodeJob job)
    {
        if (job.SubtitleBurnInStreamIndex.HasValue)
            return true;

        return !string.IsNullOrEmpty(job.VideoCodec)
            && !string.Equals(job.VideoCodec, "copy", StringComparison.OrdinalIgnoreCase);
    }
}
