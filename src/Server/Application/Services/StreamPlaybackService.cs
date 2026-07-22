using K7.Server.Application.Common;
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
using K7.Server.Domain.Extensions;
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

        if (indexedFile.FileMetadata is not VideoFileMetadata videoFileMetadata)
        {
            throw new InvalidOperationException(
                $"Indexed file '{indexedFile.Id}' has unsupported metadata type '{indexedFile.FileMetadata?.GetType().Name ?? "null"}'.");
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
        logger.LogInformation(
            "Handling segment request: Id={Id}, Quality={Quality}, SegmentNumber={SegmentNumber}",
            query.Id, query.Quality, query.SegmentNumber);

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
        if (isTransmuxing)
        {
            allSegments = query.SegmentNumber == -1
                ? hlsSegments.OrderBy(s => s.Number).Take(20).ToList()
                : hlsSegments.OrderBy(s => s.Number).ToList();
        }
        else
        {
            var totalDurationMs = hlsSegments.Count > 0
                ? hlsSegments.Sum(s => s.Duration)
                : entity.FileMetadata is VideoFileMetadata videoMetadata
                    ? (long)videoMetadata.Duration.TotalMilliseconds
                    : throw new InvalidOperationException("Cannot determine duration for HLS transcoding");
            allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);
        }

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
        var requestedIndex = query.SegmentNumber == -1 ? 0 : query.SegmentNumber;
        return await GetSegmentResultAsync(
            segmentPath, job, requestedIndex, allSegments, query.SegmentNumber, "video/mp4", cancellationToken);
    }

    public async Task<HttpContentResult> GetHlsAudioSegmentAsync(
        GetHlsAudioStreamSegmentQuery query,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Handling audio segment request: Id={Id}, AudioTrack={AudioTrack}, SegmentNumber={SegmentNumber}",
            query.Id, query.AudioTrackIndex, query.SegmentNumber);

        var entity = await context.IndexedFiles.Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);
        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        if (!new FileInfo(entity.Path).Exists)
            return new EmptyHttpContentResult(404);

        var hlsSegments = entity.FileMetadata.GetHlsSegments();
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata videoMetadata => (long)videoMetadata.Duration.TotalMilliseconds,
                AudioFileMetadata audioMetadata => (long)audioMetadata.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS audio segment")
            };
        var allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);
        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
            return new EmptyHttpContentResult(404);

        var job = await transcodeJobManager.GetOrStartJobAsync(
            query.Id, entity.Path, quality: "original", videoCodec: null, audioCodec: query.TranscodingAudioCodec,
            audioTrackIndex: query.AudioTrackIndex, isAudioOnly: true, query.StreamSessionId, cancellationToken);
        transcodeJobManager.PingJob(job.JobId, query.StreamSessionId);

        var segmentPath = Path.Combine(job.OutputDirectory, query.SegmentNumber == -1 ? "init.m4s" : $"{query.SegmentNumber}.m4s");
        var requestedIndex = query.SegmentNumber == -1 ? 0 : query.SegmentNumber;
        return await GetSegmentResultAsync(
            segmentPath, job, requestedIndex, allSegments, query.SegmentNumber, "audio/mp4", cancellationToken);
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
                if (requestedQuality.Value.Height < fileResolution.Height)
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
        logger.LogInformation(
            "Looking for segment file at: {SegmentPath} (audioOnly={IsAudioOnly}, job={JobId})",
            segmentPath,
            job.IsAudioOnly,
            job.JobId);

        // Demuxed HLS: if the paired video job already exists but segment 0 is not ready yet,
        // hold audio responses so ExoPlayer does not start parsing audio while video fMP4 is
        // still generating (PGS burn-in can take seconds). Soft-gate only - never wait
        // for a video job that has not been created yet (avoids deadlock if audio is first).
        if (job.IsAudioOnly)
            await WaitForExistingPairedVideoSegment0Async(job, cancellationToken);

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

        logger.LogInformation(
            "Serving fMP4 segment {SegmentNumber} for job {JobId}: {ByteCount} bytes, leading=[{LeadingHex}], boxes=[{Boxes}], trun=[{Trun}]",
            segmentNumber,
            job.JobId,
            segmentBytes.Length,
            HlsSegmentFileWaiter.FormatLeadingBytesHex(segmentBytes),
            HlsSegmentFileWaiter.DescribeTopLevelBoxes(segmentBytes),
            HlsSegmentFileWaiter.DescribeMoofTrun(segmentBytes));

        // Serve a snapshot - never stream a live ffmpeg output file (partial init.m4s
        // parses as ISO BMFF garbage; ExoPlayer reports "Top bit not zero").
        return new BytesHttpContentResult(segmentBytes, contentType);
    }

    private async Task WaitForExistingPairedVideoSegment0Async(
        TranscodeJob audioJob,
        CancellationToken cancellationToken)
    {
        var videoJob = transcodeJobManager.FindVideoJobForIndexedFile(audioJob.IndexedFileId);
        if (videoJob is null)
            return;

        // Hold until video init AND segment 0 are ready so ExoPlayer does not start the
        // audio MediaPeriod while video fMP4 is still mid-generate (PGS burn-in).
        if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(videoJob.OutputDirectory, 0))
            return;

        logger.LogInformation(
            "Holding audio job {AudioJobId} until paired video segment 0 is ready (videoJob={VideoJobId}, videoDir={VideoDir})",
            audioJob.JobId,
            videoJob.JobId,
            videoJob.OutputDirectory);

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HlsSegmentFileWaiter.IsSegmentReadyOnDisk(videoJob.OutputDirectory, 0))
            {
                logger.LogInformation(
                    "Paired video segment 0 ready for audio job {AudioJobId}; continuing audio serve",
                    audioJob.JobId);
                return;
            }

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
            "Timed out waiting for paired video segment 0 ({VideoDir}); serving audio anyway",
            videoJob.OutputDirectory);
    }

    private static List<HlsSegment> ComputeEqualLengthHlsSegments(long totalDurationMs, int desiredSegmentLengthMs = 6000)
    {
        var segments = new List<HlsSegment>();
        long offset = 0;
        var index = 0;
        while (offset < totalDurationMs)
        {
            var duration = Math.Min(desiredSegmentLengthMs, totalDurationMs - offset);
            segments.Add(new HlsSegment { Number = index, StartTimestamp = offset, Duration = duration });
            offset += desiredSegmentLengthMs;
            index++;
        }

        return segments;
    }
}
