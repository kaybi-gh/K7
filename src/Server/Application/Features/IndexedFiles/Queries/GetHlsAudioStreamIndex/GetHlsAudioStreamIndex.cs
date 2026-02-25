using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
//using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;

public static class GetHlsAudioStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audios/{index}/{quality}/index.m3u8";

    public static string Build(GetHlsAudioStreamIndexQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{index}", $"{query.Index}")
        .Replace("{quality}", query.AudioQualityIdentifier);

    public static string Build(Guid id, int index, string audioQualityIdentifier) => Route
        .Replace("{id}", $"{id}")
        .Replace("{index}", $"{index}")
        .Replace("{quality}", audioQualityIdentifier);

    public static string BuildManifestRelativePath(int index, string audioQualityIdentifier) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{index}", $"{index}")
        .Replace("{quality}", audioQualityIdentifier);
}
public record GetHlsAudioStreamIndexQuery(Guid Id, int Index, string AudioQualityIdentifier) : IRequest<IResult>;

public class GetHlsAudioStreamIndexQueryHandler : IRequestHandler<GetHlsAudioStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsAudioStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsAudioStreamIndexQuery query, CancellationToken cancellationToken)
    {
        if (query.AudioQualityIdentifier != "original")
        {
            var quality = Constants.AudioQualities.Where(kvp => kvp.Value.Name == query.AudioQualityIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.AudioQualityIdentifier), $"Provided quality '{query.AudioQualityIdentifier}' is not valid.");
        }        

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => x!.HlsSegments)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var indexPlaylist = GenerateHlsAudioIndexContent(entity.FileMetadata.HlsSegments);
        return Results.Content(indexPlaylist, "application/vnd.apple.mpegurl");
    }

    private static string GenerateHlsAudioIndexContent(IEnumerable<HlsSegment> hlsSegments)
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
            //content.AppendLine(GetHlsAudioStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath(segment.Number));
        }

        content.AppendLine("#EXT-X-ENDLIST");

        return content.ToString();
    }
}
