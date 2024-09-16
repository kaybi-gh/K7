using System.Text;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas.Files;
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
        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var file = new FileInfo(indexedFile.Path);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var masterPlaylist = indexedFile.FileMetadata switch
        {
            AudioFileMetadata x => throw new NotImplementedException(),
            VideoFileMetadata x => GenerateVideoFileMasterPlaylist(x),
            _ => throw new InvalidOperationException()
        };
        return Results.Content(masterPlaylist, "application/vnd.apple.mpegurl");
    }

    private string GenerateVideoFileMasterPlaylist(VideoFileMetadata videoFileMetadata)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");

        var fileResolution = videoFileMetadata.VideoResolution;

        // TODO - Create playlist depending on file original quality
        // TODO - Use quality dictionary

        //var availableTranscodingResolutions = VideoResolutions.Video.TakeWhile(x => x.Key == fileResolution);
        var availableTranscodingResolutions = Qualities.Video
            .Where((x, y) => y <= Qualities.Video.Keys.IndexOf(fileResolution))
            .Select(x => x.Value);

        foreach (var resolution in availableTranscodingResolutions)
        {
            playlist.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={resolution.AverageBitrate},RESOLUTION={resolution.Width}x{resolution.Height}");
            playlist.AppendLine($"/api/indexed-files/{videoFileMetadata.IndexedFileId}/hls-stream/{resolution.Name}/index.m3u8");
        }

        return playlist.ToString();
    }
}
