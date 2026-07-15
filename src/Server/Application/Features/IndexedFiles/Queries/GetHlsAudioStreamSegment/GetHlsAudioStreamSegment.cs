using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Extensions;
using K7.Server.Domain.Interfaces;
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
    string? TranscodingAudioCodec = null) : IRequest<HttpContentResult>;

public class GetHlsAudioStreamSegmentQueryHandler : IRequestHandler<GetHlsAudioStreamSegmentQuery, HttpContentResult>
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

    public async Task<HttpContentResult> Handle(GetHlsAudioStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling audio segment request: Id={Id}, AudioTrack={AudioTrack}, SegmentNumber={SegmentNumber}",
            query.Id,
            query.AudioTrackIndex,
            query.SegmentNumber);

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return new EmptyHttpContentResult(404);
        }

        var hlsSegments = entity.FileMetadata.GetHlsSegments();
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata v => (long)v.Duration.TotalMilliseconds,
                AudioFileMetadata a => (long)a.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS audio segment")
            };

        var allSegments = ComputeEqualLengthHlsSegments(totalDurationMs);

        if (query.SegmentNumber >= 0 && query.SegmentNumber >= allSegments.Count)
        {
            return new EmptyHttpContentResult(404);
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

        _transcodeJobManager.PingJob(job.JobId, streamSessionId);

        var segmentFileName = query.SegmentNumber == -1
            ? "init.m4s"
            : $"{query.SegmentNumber}.m4s";

        var segmentPath = Path.Combine(job.OutputDirectory, segmentFileName);
        var requestedIndex = query.SegmentNumber == -1 ? 0 : query.SegmentNumber;

        _logger.LogInformation(
            "Looking for audio segment file at: {SegmentPath}",
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
                "Audio segment {SegmentNumber} was not generated within timeout for job {JobId} (ffmpeg running: {FfmpegRunning})",
                query.SegmentNumber,
                job.JobId,
                job.FfmpegTask is { IsCompleted: false });
            return new EmptyHttpContentResult(503);
        }

        await HlsSegmentFileWaiter.WaitUntilReadableAsync(segmentPath, cancellationToken);

        return new FileHttpContentResult(segmentPath, "audio/mp4");
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
