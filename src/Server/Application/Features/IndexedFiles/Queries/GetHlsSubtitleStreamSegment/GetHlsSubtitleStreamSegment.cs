using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamSegment;

public static class GetHlsSubtitleStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/subtitles/{subtitleTrackIndex}/segments/{segmentNumber}.vtt";

    public static string Build(Guid id, int subtitleTrackIndex, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}")
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.vtt";
}

public record GetHlsSubtitleStreamSegmentQuery(
    Guid Id,
    int SubtitleTrackIndex,
    int SegmentNumber,
    Guid StreamSessionId) : IRequest<HttpContentResult>;

public class GetHlsSubtitleStreamSegmentQueryHandler : IRequestHandler<GetHlsSubtitleStreamSegmentQuery, HttpContentResult>
{
    private const int SubtitleSegmentDurationSeconds = 30;

    private readonly IApplicationDbContext _context;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly ILogger<GetHlsSubtitleStreamSegmentQueryHandler> _logger;
    private readonly string _transcodingPath;

    public GetHlsSubtitleStreamSegmentQueryHandler(
        IApplicationDbContext context,
        IMediaTranscoder mediaTranscoder,
        ILogger<GetHlsSubtitleStreamSegmentQueryHandler> logger,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _context = context;
        _mediaTranscoder = mediaTranscoder;
        _logger = logger;
        _transcodingPath = pathsOptions.Value.Transcoding
            ?? throw new InvalidOperationException("Transcoding path not configured");
    }

    public async Task<HttpContentResult> Handle(GetHlsSubtitleStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
            return new EmptyHttpContentResult(404);

        var vttCachePath = HlsSubtitleVttExtractor.GetCachePath(
            _transcodingPath,
            entity.Id,
            query.SubtitleTrackIndex);

        if (!HlsSubtitleVttExtractor.IsReady(vttCachePath))
        {
            _logger.LogDebug(
                "Subtitle VTT cache miss for track {Track} - extract in background, not blocking A/V",
                query.SubtitleTrackIndex);
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                _mediaTranscoder,
                entity.Path,
                query.SubtitleTrackIndex,
                vttCachePath,
                _logger);
            // Do not return empty 200: ExoPlayer caches that and never shows cues.
            return new TextHttpContentResult(
                "Subtitle extract in progress",
                "text/plain",
                503);
        }

        var fullVtt = await File.ReadAllTextAsync(vttCachePath, cancellationToken);
        var startTimeSeconds = query.SegmentNumber * SubtitleSegmentDurationSeconds;
        var endTimeSeconds = startTimeSeconds + SubtitleSegmentDurationSeconds;
        var segmentVtt = WebVttSegmenter.ExtractSegment(fullVtt, startTimeSeconds, endTimeSeconds);

        return new TextHttpContentResult(segmentVtt, "text/vtt; charset=utf-8");
    }
}
