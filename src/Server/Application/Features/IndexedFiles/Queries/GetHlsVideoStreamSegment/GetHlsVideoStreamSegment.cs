using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
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
    int? MuxedAudioTrackIndex = null,
    string? MuxedAudioCodec = null) : IRequest<IResult>;

public class GetHlsVideoStreamSegmentQueryHandler : IRequestHandler<GetHlsVideoStreamSegmentQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ITranscodeJobManager _transcodeJobManager;
    private readonly ILogger<GetHlsVideoStreamSegmentQueryHandler> _logger;

    public GetHlsVideoStreamSegmentQueryHandler(
        IApplicationDbContext context,
        ITranscodeJobManager transcodeJobManager,
        ILogger<GetHlsVideoStreamSegmentQueryHandler> logger)
    {
        _context = context;
        _transcodeJobManager = transcodeJobManager;
        _logger = logger;
    }

    public async Task<IResult> Handle(GetHlsVideoStreamSegmentQuery query, CancellationToken cancellationToken)
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
        
        if (query.SegmentNumber != -1)
        {
            query_db = query_db.Include(x => x.FileMetadata!.HlsSegments);
        }
        
        var entity = await query_db.FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound("Source file not found");
        }

        var isTransmuxing = query.Quality == "original";
        List<HlsSegment> allSegments;

        if (isTransmuxing)
        {
            // Transmux (stream copy): use keyframe-based segments from the database
            if (query.SegmentNumber == -1)
            {
                allSegments = await _context.HlsSegments
                    .Where(s => s.FileMetadataId == entity.FileMetadata.Id)
                    .OrderBy(s => s.Number)
                    .Take(20)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                Guard.Against.NullOrEmpty(entity.FileMetadata.HlsSegments);
                allSegments = entity.FileMetadata.HlsSegments.OrderBy(s => s.Number).ToList();
            }
        }
        else
        {
            // Transcode: use equal-length 6s segments matching the index playlist
            var dbSegments = query.SegmentNumber == -1
                ? await _context.HlsSegments
                    .Where(s => s.FileMetadataId == entity.FileMetadata.Id)
                    .ToListAsync(cancellationToken)
                : entity.FileMetadata.HlsSegments?.ToList()
                    ?? await _context.HlsSegments
                        .Where(s => s.FileMetadataId == entity.FileMetadata.Id)
                        .ToListAsync(cancellationToken);

            var totalDurationMs = dbSegments.Sum(s => s.Duration);
            allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);
        }

        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
        {
            return Results.NotFound($"Segment index {query.SegmentNumber} out of range (0-{allSegments.Count - 1})");
        }

        var videoCodec = query.TranscodingVideoCodec;

        var streamSessionId = query.StreamSessionId;
        var job = await _transcodeJobManager.GetOrStartJobAsync(
            query.Id,
            entity.Path,
            query.Quality,
            videoCodec,
            audioCodec: query.MuxedAudioTrackIndex.HasValue ? (query.MuxedAudioCodec ?? "copy") : null,
            audioTrackIndex: query.MuxedAudioTrackIndex ?? 0,
            isAudioOnly: false,
            streamSessionId,
            cancellationToken);

        if (query.SegmentNumber == -1)
        {
            await _transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
                job.JobId,
                0,
                allSegments,
                cancellationToken);
        }
        else
        {
            await _transcodeJobManager.EnsureSegmentWillBeGeneratedAsync(
                job.JobId,
                query.SegmentNumber,
                allSegments,
                cancellationToken);
        }

        _transcodeJobManager.PingJob(job.JobId, streamSessionId);

        var segmentFileName = query.SegmentNumber == -1
            ? "init.m4s"
            : $"{query.SegmentNumber}.m4s";

        var segmentPath = Path.Combine(job.OutputDirectory, segmentFileName);

        _logger.LogInformation(
            "Looking for segment file at: {SegmentPath}",
            segmentPath);

        var timeoutSeconds = query.SegmentNumber == -1 ? 60 : 30;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var pollingInterval = 200;
        
        while (!File.Exists(segmentPath) && DateTime.UtcNow < deadline)
        {
            if (DateTime.UtcNow.Second % 2 == 0 && DateTime.UtcNow.Millisecond < pollingInterval)
            {
                _logger.LogDebug(
                    "Waiting for segment {SegmentNumber} to be generated for job {JobId}",
                    query.SegmentNumber,
                    job.JobId);
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }

        if (!File.Exists(segmentPath))
        {
            _logger.LogError(
                "Segment {SegmentNumber} was not generated within timeout for job {JobId}",
                query.SegmentNumber,
                job.JobId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Wait for the file to be accessible (not locked by FFmpeg)
        FileStream? stream = null;
        var accessDeadline = DateTime.UtcNow.AddSeconds(5);
        
        while (stream == null && DateTime.UtcNow < accessDeadline)
        {
            try
            {
                stream = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                // File is still locked by FFmpeg, wait and retry
                await Task.Delay(100, cancellationToken);
            }
        }
        
        if (stream == null)
        {
            _logger.LogError(
                "Segment {SegmentNumber} file is locked and could not be accessed for job {JobId}",
                query.SegmentNumber,
                job.JobId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Stream(stream, "video/mp4", enableRangeProcessing: true);
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
