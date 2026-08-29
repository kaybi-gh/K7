using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;

public static class GetHlsAudioStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audio/{audioTrackIndex}/index.m3u8";

    public static string Build(Guid id, int audioTrackIndex) => Route
        .Replace("{id}", $"{id}")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}");

    /// <summary>
    /// Builds a path relative to the master manifest location for use in #EXT-X-MEDIA URI.
    /// </summary>
    public static string BuildManifestRelativePath(int audioTrackIndex) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}");
}

public record GetHlsAudioStreamIndexQuery(
    Guid Id,
    int AudioTrackIndex,
    Guid StreamSessionId,
    string? TranscodingAudioCodec = null,
    double? StartSeconds = null) : IRequest<HttpContentResult>;

public class GetHlsAudioStreamIndexQueryHandler : IRequestHandler<GetHlsAudioStreamIndexQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsAudioStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HttpContentResult> Handle(GetHlsAudioStreamIndexQuery query, CancellationToken cancellationToken)
    {
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

        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(_context, query.Id, cancellationToken);
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata v => (long)v.Duration.TotalMilliseconds,
                AudioFileMetadata a => (long)a.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS audio stream")
            };

        // Same keyframe timeline as video so track switches stay segment-aligned.
        var streamingSegments = HlsSegmentHelper.ResolveStreamingSegments(hlsSegments, totalDurationMs);
        var segmentDurations = HlsSegmentHelper.ToDurationSeconds(streamingSegments);

        var queryString = HlsMediaPlaylistBuilder.BuildQueryString(
            query.StreamSessionId,
            ("TranscodingAudioCodec", query.TranscodingAudioCodec));

        var indexPlaylist = HlsMediaPlaylistBuilder.Build(
            segmentDurations,
            queryString,
            GetHlsAudioStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath,
            query.StartSeconds,
            independentSegments: !string.IsNullOrEmpty(query.TranscodingAudioCodec));

        return new TextHttpContentResult(indexPlaylist, "application/vnd.apple.mpegurl");
    }
}
