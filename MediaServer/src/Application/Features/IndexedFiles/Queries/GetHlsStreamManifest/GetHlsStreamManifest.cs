using System.Text;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStream;
using MediaServer.Domain.Constants;
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

        await LoadFileTracksAsync(indexedFile.FileMetadata, cancellationToken);

        var masterPlaylist = indexedFile.FileMetadata switch
        {
            AudioFileMetadata x => throw new NotImplementedException(),
            VideoFileMetadata x => GenerateVideoFileMasterPlaylist(x),
            _ => throw new InvalidOperationException()
        };
        return Results.Content(masterPlaylist, "application/vnd.apple.mpegurl");
    }

    private async Task LoadFileTracksAsync(BaseFileMetadata? fileMetadata, CancellationToken cancellationToken)
    {
        switch (fileMetadata)
        {
            case VideoFileMetadata videoMetadata:
                await _context.Entry(videoMetadata).Collection(v => v.AudioTracks).LoadAsync(cancellationToken);
                await _context.Entry(videoMetadata).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
                break;
            case AudioFileMetadata audioMetadata:
                await _context.Entry(audioMetadata).Reference(a => a.AudioTrack).LoadAsync(cancellationToken);
                break;
        }
    }

    private string GenerateVideoFileMasterPlaylist(VideoFileMetadata videoFileMetadata)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:4");
        playlist.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.AppendLine();

        var fileResolutionIdentifier = videoFileMetadata.VideoResolution;
        var fileResolution = Qualities.Video.Single(x => x.Key == fileResolutionIdentifier).Value;

        //var availableTranscodingResolutions = VideoResolutions.Video.TakeWhile(x => x.Key == fileResolution);
        var availableTranscodingResolutions = Qualities.Video
            .Where((x, y) => y <= Qualities.Video.Keys.IndexOf(fileResolutionIdentifier))
            .Reverse()
            .Select(x => x.Value);

        // Add original stream
        playlist.AppendLine("# Video playlists");
        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH={fileResolution.MaxBitrate}," +
            $"AVERAGE-BANDWIDTH={fileResolution.AverageBitrate}," +
            $"RESOLUTION={fileResolution.Width}x{fileResolution.Height}," +
            $"CODECS=\"{HlsCodecStringHelpers.GetHlsCodecs(videoFileMetadata)}\"," +
            $"AUDIO=\"audio\"");
        playlist.AppendLine(GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath("original"));

        // Add transcoded streams
        foreach (var resolution in availableTranscodingResolutions)
        {
            playlist.AppendLine($"#EXT-X-STREAM-INF:" +
                $"BANDWIDTH={resolution.MaxBitrate}," +
                $"AVERAGE-BANDWIDTH={resolution.AverageBitrate}," +
                $"RESOLUTION={resolution.Width}x{resolution.Height}," +
                $"CODECS=\"avc1.640028,mp4a.40.2\"," +
                $"AUDIO=\"audio\"");
            playlist.AppendLine(GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath(resolution.Name));
        }
        playlist.AppendLine();

        // Add audio streams
        // TODO - Cleanup useless and unusable streams
        playlist.AppendLine("# Audio playlists");
        foreach (var audioStream in videoFileMetadata.AudioTracks)
        {
            playlist.AppendLine(
                $"#EXT-X-MEDIA:" +
                $"TYPE=AUDIO," +
                $"GROUP-ID=\"audio\"," +
                $"NAME=\"{audioStream.Name}\"," +
                $"DEFAULT={(audioStream.IsDefault ? "YES" : "NO")}," +
                $"AUTOSELECT=YES," +
                $"LANGUAGE=\"{audioStream.Language}\"," +
                $"URI={GetHlsAudioStreamIndexQueryUriBuilder.BuildManifestRelativePath(audioStream.Index, "original")}"
            );

            playlist.AppendLine(
                $"#EXT-X-MEDIA:" +
                $"TYPE=AUDIO," +
                $"GROUP-ID=\"audio\"," +
                $"NAME=\"{audioStream.Name} transcode\"," +
                $"DEFAULT={(audioStream.IsDefault ? "YES" : "NO")}," +
                $"AUTOSELECT=YES," +
                $"LANGUAGE=\"{audioStream.Language}\"," +
                $"URI={GetHlsAudioStreamIndexQueryUriBuilder.BuildManifestRelativePath(audioStream.Index, "LowAac")}"
            );
        }

        return playlist.ToString();
    }
}
