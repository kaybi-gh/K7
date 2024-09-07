using System.Text;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStream;

public record GetHlsStreamIndexQuery(Guid Id, string VideoQuality) : IRequest<IResult>;

public class GetHlsStreamIndexQueryHandler : IRequestHandler<GetHlsStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsStreamIndexQuery query, CancellationToken cancellationToken)
    {
        var quality = Qualities.Video.Where(kvp => kvp.Value.Name == query.VideoQuality).FirstOrDefault();
        Guard.Against.Null(quality, nameof(quality), $"Provided quality '{query.VideoQuality}' is not valid.");

        var entity = await _context.IndexedFiles
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        // TODO - Get file qualities
        var qualities = new List<string>() { "720p", "1080p" }.ToArray();
        var masterPlaylist = GenerateHlsIndexContent($"{query.Id}", qualities);

        return Results.Content(masterPlaylist, "application/vnd.apple.mpegurl");
    }

    private static string GenerateHlsIndexContent(string indexedFileId, string[] qualities)
    {
        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");

        foreach (var resolution in qualities)
        {
            //content.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={GetBitrateForResolution(resolution)},RESOLUTION={resolution}");
            content.AppendLine($"/api/files/{indexedFileId}/hls-stream/{resolution}");
        }

        return content.ToString();
    }
}
