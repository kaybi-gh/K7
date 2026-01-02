using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;


public static class GetHlsVideoStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/index.m3u8";

    public static string Build(GetHlsVideoStreamIndexQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.VideoResolutionIdentifier);

    public static string Build(Guid id, string videoResolutionIdentifier) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", videoResolutionIdentifier);

    public static string BuildManifestRelativePath(string videoResolutionIdentifier) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{quality}", $"{videoResolutionIdentifier}");
}
public record GetHlsVideoStreamIndexQuery(Guid Id, string VideoResolutionIdentifier) : IRequest<IResult>;

public class GetHlsVideoStreamIndexQueryHandler : IRequestHandler<GetHlsVideoStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsVideoStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsVideoStreamIndexQuery query, CancellationToken cancellationToken)
    {
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Constants.VideoQualities.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.VideoResolutionIdentifier), $"Provided quality '{query.VideoResolutionIdentifier}' is not valid.");
        }        

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
            return Results.NotFound();
        }

        var indexPlaylist = GenerateHlsIndexContent(entity.FileMetadata.HlsSegments);
        return Results.Content(indexPlaylist, "application/vnd.apple.mpegurl");
    }

    private static string GenerateHlsIndexContent(IEnumerable<HlsSegment> hlsSegments)
    {
        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");
        content.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        content.AppendLine($"#EXT-X-TARGETDURATION:{Math.Ceiling(hlsSegments.Max(x => x.Duration) / 1000.0)}");
        content.AppendLine("#EXT-X-VERSION:4"); // TODO - Use the right version
        content.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        content.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");

        foreach (var segment in hlsSegments)
        {
            content.AppendLine($"#EXTINF:{(segment.Duration / 1000.0).ToString("F6", CultureInfo.InvariantCulture)},");
            content.AppendLine(GetHlsVideoStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath(segment.Number));
        }

        content.AppendLine("#EXT-X-ENDLIST");

        return content.ToString();
    }
}
