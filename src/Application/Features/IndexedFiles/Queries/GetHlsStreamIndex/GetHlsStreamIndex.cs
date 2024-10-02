using System.Globalization;
using System.Text;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamSegment;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStream;

public record GetHlsStreamIndexQuery(Guid Id, string VideoResolutionIdentifier) : IRequest<IResult>;

public class GetHlsStreamIndexQueryHandler : IRequestHandler<GetHlsStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsStreamIndexQuery query, CancellationToken cancellationToken)
    {
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Qualities.Video.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
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
