using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;

public static class GetHlsAudioStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audio/{audioTrackIndex}/segments/{segmentNumber}.m4s";

    public static string Build(Guid id, int audioTrackIndex, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}")
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.m4s";
}

public record GetHlsAudioStreamSegmentQuery(
    Guid Id,
    int AudioTrackIndex,
    int SegmentNumber,
    Guid StreamSessionId,
    string? TranscodingAudioCodec = null) : IRequest<IResult>;

public class GetHlsAudioStreamSegmentQueryHandler : IRequestHandler<GetHlsAudioStreamSegmentQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ITranscodeJobManager _transcodeJobManager;
    private readonly ILogger<GetHlsAudioStreamSegmentQueryHandler> _logger;

    public GetHlsAudioStreamSegmentQueryHandler(
        IApplicationDbContext context,
        ITranscodeJobManager transcodeJobManager,
        ILogger<GetHlsAudioStreamSegmentQueryHandler> logger)
    {
        _context = context;
        _transcodeJobManager = transcodeJobManager;
        _logger = logger;
    }

    public async Task<IResult> Handle(GetHlsAudioStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling audio segment request: Id={Id}, AudioTrack={AudioTrack}, SegmentNumber={SegmentNumber}",
            query.Id,
            query.AudioTrackIndex,
            query.SegmentNumber);

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => x!.HlsSegments)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);
        Guard.Against.NullOrEmpty(entity.FileMetadata.HlsSegments);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound("Source file not found");
        }

        var totalDurationMs = entity.FileMetadata.HlsSegments.Sum(s => s.Duration);
        var allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);

        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
        {
            return Results.NotFound($"Segment index {query.SegmentNumber} out of range (0-{allSegments.Count - 1})");
        }

        var audioCodec = query.TranscodingAudioCodec;
        var streamSessionId = query.StreamSessionId;

        var job = await _transcodeJobManager.GetOrStartJobAsync(
            query.Id,
            entity.Path,
            quality: "original",
            videoCodec: null,
            audioCodec: audioCodec,
            audioTrackIndex: query.AudioTrackIndex,
            isAudioOnly: true,
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
            "Looking for audio segment file at: {SegmentPath}",
            segmentPath);

        var timeoutSeconds = query.SegmentNumber == -1 ? 60 : 30;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var pollingInterval = 200;

        while (!File.Exists(segmentPath) && DateTime.UtcNow < deadline)
        {
            if (DateTime.UtcNow.Second % 2 == 0 && DateTime.UtcNow.Millisecond < pollingInterval)
            {
                _logger.LogDebug(
                    "Waiting for audio segment {SegmentNumber} to be generated for job {JobId}",
                    query.SegmentNumber,
                    job.JobId);
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }

        if (!File.Exists(segmentPath))
        {
            _logger.LogError(
                "Audio segment {SegmentNumber} was not generated within timeout for job {JobId}",
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
                await Task.Delay(100, cancellationToken);
            }
        }

        if (stream == null)
        {
            _logger.LogError(
                "Audio segment {SegmentNumber} file is locked and could not be accessed for job {JobId}",
                query.SegmentNumber,
                job.JobId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Stream(stream, "audio/mp4", enableRangeProcessing: true);
    }

    /// <summary>
    /// Computes equal-length synthetic HlsSegments that match the audio playlist layout.
    /// Audio can be split at any frame boundary, so we use fixed 6-second segments
    /// instead of keyframe-based segments from the video track.
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
