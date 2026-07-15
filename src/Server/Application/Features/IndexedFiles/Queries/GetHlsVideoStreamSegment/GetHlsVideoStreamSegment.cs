using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;

public static class GetHlsVideoStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/segments/{segmentNumber}.m4s";

    public static string Build(GetHlsVideoStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.Quality)
        .Replace("{segmentNumber}", query.SegmentNumber.ToString());

    public static string Build(Guid id, string quality, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", quality)
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.m4s";

    public static string BuildInitSegmentPath() => "segments/init.m4s";
}

public record GetHlsVideoStreamSegmentQuery(
    Guid Id,
    string Quality,
    int SegmentNumber,
    Guid StreamSessionId,
    string? TranscodingVideoCodec = null,
    int? SubtitleBurnInStreamIndex = null) : IRequest<HttpContentResult>;

public class GetHlsVideoStreamSegmentQueryHandler : IRequestHandler<GetHlsVideoStreamSegmentQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ITranscodeJobManager _transcodeJobManager;
    private readonly IActiveStreamTracker _activeStreamTracker;
    private readonly IFfmpegCapabilitiesService _ffmpegCapabilitiesService;
    private readonly ISender _sender;
    private readonly ILogger<GetHlsVideoStreamSegmentQueryHandler> _logger;

    public GetHlsVideoStreamSegmentQueryHandler(
        IApplicationDbContext context,
        ITranscodeJobManager transcodeJobManager,
        IActiveStreamTracker activeStreamTracker,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        ISender sender,
        ILogger<GetHlsVideoStreamSegmentQueryHandler> logger)
    {
        _context = context;
        _transcodeJobManager = transcodeJobManager;
        _activeStreamTracker = activeStreamTracker;
        _ffmpegCapabilitiesService = ffmpegCapabilitiesService;
        _sender = sender;
        _logger = logger;
    }

    public async Task<HttpContentResult> Handle(GetHlsVideoStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling segment request: Id={Id}, Quality={Quality}, SegmentNumber={SegmentNumber}",
            query.Id,
            query.Quality,
            query.SegmentNumber);

        if (query.Quality != "original")
        {
            var qualityDef = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
            Guard.Against.Null(qualityDef, nameof(query.Quality), $"Provided quality '{query.Quality}' is not valid.");
        }

        var query_db = _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .Where(x => x.Id == query.Id);

        var entity = await query_db.FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return new EmptyHttpContentResult(404);
        }

        var isTransmuxing = query.Quality == "original"
            && string.IsNullOrEmpty(query.TranscodingVideoCodec)
            && !query.SubtitleBurnInStreamIndex.HasValue;
        List<HlsSegment> allSegments;

        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(_context, query.Id, cancellationToken);
        var effectiveTranscodingVideoCodec = query.TranscodingVideoCodec;

        if (isTransmuxing && hlsSegments.Count == 0)
        {
            await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(
                _sender,
                query.Id,
                _logger,
                cancellationToken);

            isTransmuxing = false;
            effectiveTranscodingVideoCodec ??= HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        if (isTransmuxing)
        {
            // Transmux (stream copy): use keyframe-based segments from the database
            if (query.SegmentNumber == -1)
            {
                allSegments = hlsSegments
                    .OrderBy(s => s.Number)
                    .Take(20)
                    .ToList();
            }
            else
            {
                allSegments = hlsSegments.OrderBy(s => s.Number).ToList();
            }
        }
        else
        {
            // Transcode: use equal-length 6s segments matching the index playlist
            var totalDurationMs = hlsSegments.Count > 0
                ? hlsSegments.Sum(s => s.Duration)
                : entity.FileMetadata is VideoFileMetadata v
                    ? (long)v.Duration.TotalMilliseconds
                    : throw new InvalidOperationException("Cannot determine duration for HLS transcoding");

            allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);
        }

        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
        {
            return new EmptyHttpContentResult(404);
        }

        var videoCodec = effectiveTranscodingVideoCodec
            ?? (query.SubtitleBurnInStreamIndex.HasValue ? "h264" : null);

        if (query.Quality != "original" && entity.FileMetadata is VideoFileMetadata videoMetadataForQuality)
        {
            var requestedQuality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
            if (requestedQuality.Value is not null)
            {
                var fileResolution = Constants.VideoQualities.Single(x => x.Key == videoMetadataForQuality.VideoResolution).Value;
                if (requestedQuality.Value.Height < fileResolution.Height)
                {
                    await _context.Entry(videoMetadataForQuality).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
                    var videoTrack = videoMetadataForQuality.VideoTracks
                        .OrderByDescending(t => t.IsDefault)
                        .ThenBy(t => t.Index)
                        .FirstOrDefault();

                    var sourceResolution = videoTrack is { Width: > 0, Height: > 0 }
                        ? $"{videoTrack.Width}x{videoTrack.Height}"
                        : $"{fileResolution.Width}x{fileResolution.Height}";

                    videoCodec ??= "h264";

                    var existing = _activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
                    _activeStreamTracker.UpdateStreamDecision(
                        query.StreamSessionId,
                        StreamDecisionExtensions.ApplyQualityDownscale(
                            existing,
                            requestedQuality.Value,
                            videoCodec,
                            sourceResolution));
                }
            }
        }

        if (query.SubtitleBurnInStreamIndex is int burnInIndex
            && entity.FileMetadata is VideoFileMetadata videoMetadata)
        {
            await _context.Entry(videoMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

            var burnInTrack = videoMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == burnInIndex);
            if (burnInTrack is not null)
            {
                var existing = _activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
                _activeStreamTracker.UpdateStreamDecision(
                    query.StreamSessionId,
                    StreamDecisionExtensions.ApplySubtitleBurnIn(existing, burnInTrack));
            }
            else
            {
                _logger.LogWarning(
                    "Subtitle burn-in stream index {StreamIndex} not found among subtitle tracks for IndexedFile {Id}",
                    burnInIndex,
                    query.Id);
            }
        }

        await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(
            query.StreamSessionId,
            _activeStreamTracker,
            _ffmpegCapabilitiesService,
            cancellationToken);

        var streamSessionId = query.StreamSessionId;
        var job = await _transcodeJobManager.GetOrStartJobAsync(
            query.Id,
            entity.Path,
            query.Quality,
            videoCodec,
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            streamSessionId,
            cancellationToken,
            query.SubtitleBurnInStreamIndex);

        _transcodeJobManager.PingJob(job.JobId, streamSessionId);

        var segmentFileName = query.SegmentNumber == -1
            ? "init.m4s"
            : $"{query.SegmentNumber}.m4s";

        var segmentPath = Path.Combine(job.OutputDirectory, segmentFileName);
        var requestedIndex = query.SegmentNumber == -1 ? 0 : query.SegmentNumber;

        _logger.LogInformation(
            "Looking for segment file at: {SegmentPath}",
            segmentPath);

        if (!await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
                segmentPath,
                job,
                ct => _transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
                    job.JobId,
                    requestedIndex,
                    allSegments,
                    ct),
                cancellationToken,
                maxTotalSeconds: query.SegmentNumber == -1 ? 90 : 180))
        {
            _logger.LogError(
                "Segment {SegmentNumber} was not generated within timeout for job {JobId} (ffmpeg running: {FfmpegRunning})",
                query.SegmentNumber,
                job.JobId,
                job.FfmpegTask is { IsCompleted: false });
            return new EmptyHttpContentResult(503);
        }

        await HlsSegmentFileWaiter.WaitUntilReadableAsync(segmentPath, cancellationToken);

        return new FileHttpContentResult(segmentPath, "video/mp4");
    }

    /// <summary>
    /// Computes equal-length synthetic HlsSegments that match the index playlist layout
    /// when transcoding. Transcoded video can be split at any frame boundary, so we use
    /// fixed 6-second segments instead of keyframe-based segments from the source file.
    /// </summary>
    private static List<HlsSegment> ComputeEqualLengthHlsSegments(long totalDurationMs, int desiredSegmentLengthMs = 6000)
    {
        var segments = new List<HlsSegment>();
        long offset = 0;
        var index = 0;

        while (offset < totalDurationMs)
        {
            var duration = Math.Min(desiredSegmentLengthMs, totalDurationMs - offset);
            segments.Add(new HlsSegment
            {
                Number = index,
                StartTimestamp = offset,
                Duration = duration
            });
            offset += desiredSegmentLengthMs;
            index++;
        }

        return segments;
    }
}
