using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;

public static class GetHlsStreamManifestQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/hls-stream/manifest.m3u8";

    public static string Build(GetHlsStreamManifestQuery query)
    {
        var route = Route.Replace("{id}", $"{query.Id}");

        var queryParams = new Dictionary<string, string?>
        {
            { nameof(query.StreamSessionId), query.StreamSessionId.ToString() },
            { nameof(query.AudioTrackIndex), query.AudioTrackIndex.ToString() },
            { nameof(query.TranscodingAudioCodec), query.TranscodingAudioCodec },
            { nameof(query.TranscodingVideoCodec), query.TranscodingVideoCodec }
        };

        var filteredParams = queryParams
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .ToDictionary(x => x.Key, x => x.Value!);

        return filteredParams.Count > 0
            ? QueryHelpers.AddQueryString(route, filteredParams!)
            : route;
    }

    // TODO - Use Build with original query params
    public static string Build(Guid id) => Route
        .Replace("{id}", $"{id}");
}

public record GetHlsStreamManifestQuery : IRequest<IResult>
{
    public required Guid Id { get; set; }
    public required Guid StreamSessionId { get; set; }
    public required int AudioTrackIndex { get; set; }
    public string? TranscodingAudioCodec { get; set; } = null;
    public string? TranscodingVideoCodec { get; set; } = null;
    // TODO - Add container
};

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
            VideoFileMetadata x => GenerateVideoFileMasterPlaylist(x, query),
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

    private string GenerateVideoFileMasterPlaylist(VideoFileMetadata videoFileMetadata, GetHlsStreamManifestQuery query)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");

        var fileResolutionIdentifier = videoFileMetadata.VideoResolution;
        var fileResolution = Constants.VideoQualities.Single(x => x.Key == fileResolutionIdentifier).Value;

        var originalCodecs = HlsCodecStringHelpers.GetHlsCodecs(videoFileMetadata);

        var originalVideoCodec = string.Empty;
        var originalAudioCodec = string.Empty;

        if (!string.IsNullOrWhiteSpace(originalCodecs))
        {
            var parts = originalCodecs.Split(',', 2);
            originalVideoCodec = parts[0];
            if (parts.Length > 1)
            {
                originalAudioCodec = parts[1];
            }
        }

        var hasAudioTranscoding = !string.IsNullOrWhiteSpace(query.TranscodingAudioCodec);
        var hasVideoTranscoding = !string.IsNullOrWhiteSpace(query.TranscodingVideoCodec);

        string codecsAttribute;

        if (!hasAudioTranscoding && !hasVideoTranscoding)
        {
            codecsAttribute = originalCodecs;
        }
        else
        {
            var effectiveVideoCodecId = hasVideoTranscoding
                ? query.TranscodingVideoCodec
                : originalVideoCodec;

            var effectiveAudioCodecId = hasAudioTranscoding
                ? query.TranscodingAudioCodec
                : originalAudioCodec;

            var transcodingCodecs = HlsCodecStringHelpers.GetHlsCodecs(effectiveVideoCodecId, effectiveAudioCodecId);

            codecsAttribute = string.IsNullOrWhiteSpace(transcodingCodecs)
                ? originalCodecs
                : transcodingCodecs;
        }

        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH={fileResolution.MaxBitrate}," +
            $"AVERAGE-BANDWIDTH={fileResolution.AverageBitrate}," +
            $"RESOLUTION={fileResolution.Width}x{fileResolution.Height}," +
            $"CODECS=\"{codecsAttribute}\"");

        var playlistQuality = hasVideoTranscoding ? fileResolution.Name : "original";
        var playlistUrl = GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath(playlistQuality);
        
        // Add parameters to the playlist URL
        var queryParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}",
            $"audioTrackIndex={query.AudioTrackIndex}"
        };
        
        if (!string.IsNullOrEmpty(query.TranscodingVideoCodec))
            queryParams.Add($"TranscodingVideoCodec={query.TranscodingVideoCodec}");
        if (!string.IsNullOrEmpty(query.TranscodingAudioCodec))
            queryParams.Add($"TranscodingAudioCodec={query.TranscodingAudioCodec}");
            
        var queryString = "?" + string.Join("&", queryParams);
        playlistUrl += queryString;
        
        playlist.AppendLine(playlistUrl);
        playlist.AppendLine();

        return playlist.ToString();
    }
}
