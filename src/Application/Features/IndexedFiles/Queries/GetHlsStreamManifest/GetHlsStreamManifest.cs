using System.Text;
using MediaServer.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;

public record GetHlsStreamManifestQuery(Guid Id) : IRequest<IResult>;

public class GetHlsStreamManifestQueryHandler : IRequestHandler<GetHlsStreamManifestQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsStreamManifestQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsStreamManifestQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var masterPlaylist = GenerateMasterPlaylist(query.Id);
        return Results.Content(masterPlaylist, "application/vnd.apple.mpegurl");
    }

    private string GenerateMasterPlaylist(Guid id)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");

        // TODO - Create playlist depending on file original quality
        // TODO - Use quality dictionary

        // Transcoded qualities
        //var qualities = new List<string>() { "720p", "1080p" };

        var renditions = new List<(string Quality, int Bandwidth, string Resolution)>
        {
            ("720p", 3000000, "1280x720"),
            ("480p", 1500000, "854x480"),
            ("360p", 800000, "640x360")
        };

        foreach (var rendition in renditions)
        {
            playlist.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={rendition.Bandwidth},RESOLUTION={rendition.Resolution}");
            playlist.AppendLine($"/api/indexed-files/{id}/hls-stream/{rendition.Quality}/index.m3u8");
        }

        return playlist.ToString();
    }
}
