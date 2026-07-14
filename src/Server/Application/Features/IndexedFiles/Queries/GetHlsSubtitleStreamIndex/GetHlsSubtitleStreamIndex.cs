using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamSegment;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Extensions;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;

public static class GetHlsSubtitleStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/subtitles/{subtitleTrackIndex}/index.m3u8";

    public static string Build(Guid id, int subtitleTrackIndex) => Route
        .Replace("{id}", $"{id}")
        .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}");

    /// <summary>
    /// Builds a path relative to the master manifest location for use in #EXT-X-MEDIA URI.
    /// </summary>
    public static string BuildManifestRelativePath(int subtitleTrackIndex) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}");
}

public record GetHlsSubtitleStreamIndexQuery(
    Guid Id,
    int SubtitleTrackIndex,
    Guid StreamSessionId) : IRequest<HttpContentResult>;

public class GetHlsSubtitleStreamIndexQueryHandler : IRequestHandler<GetHlsSubtitleStreamIndexQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsSubtitleStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HttpContentResult> Handle(GetHlsSubtitleStreamIndexQuery query, CancellationToken cancellationToken)
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

        var hlsSegments = entity.FileMetadata.GetHlsSegments();
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata v => (long)v.Duration.TotalMilliseconds,
                AudioFileMetadata a => (long)a.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS subtitle stream")
            };

        var indexPlaylist = GenerateSubtitlePlaylist(totalDurationMs, query.StreamSessionId);

        return new TextHttpContentResult(indexPlaylist, "application/vnd.apple.mpegurl");
    }

    private const int SubtitleSegmentDurationSeconds = 30;

    private static string GenerateSubtitlePlaylist(long totalDurationMs, Guid streamSessionId)
    {
        var totalDurationSeconds = totalDurationMs / 1000.0;
        var segmentCount = (int)Math.Ceiling(totalDurationSeconds / SubtitleSegmentDurationSeconds);

        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");
        content.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        content.AppendLine($"#EXT-X-TARGETDURATION:{SubtitleSegmentDurationSeconds}");
        content.AppendLine("#EXT-X-VERSION:3");
        content.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

        var queryString = $"?streamSessionId={streamSessionId}";

        for (int i = 0; i < segmentCount; i++)
        {
            var segmentStart = i * SubtitleSegmentDurationSeconds;
            var segmentDuration = Math.Min(SubtitleSegmentDurationSeconds, totalDurationSeconds - segmentStart);

            content.AppendLine($"#EXTINF:{segmentDuration.ToString("F6", CultureInfo.InvariantCulture)},");
            content.AppendLine($"{GetHlsSubtitleStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath(i)}{queryString}");
        }

        content.AppendLine("#EXT-X-ENDLIST");

        return content.ToString();
    }
}
