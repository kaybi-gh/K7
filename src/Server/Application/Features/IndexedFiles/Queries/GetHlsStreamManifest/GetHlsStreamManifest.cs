using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
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
            { nameof(query.TranscodingAudioCodec), query.TranscodingAudioCodec },
            { nameof(query.TranscodingVideoCodec), query.TranscodingVideoCodec },
            { nameof(query.DefaultAudioTrackIndex), query.DefaultAudioTrackIndex?.ToString() }
        };

        var filteredParams = queryParams
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .ToDictionary(x => x.Key, x => x.Value!);

        return filteredParams.Count > 0
            ? QueryHelpers.AddQueryString(route, filteredParams!)
            : route;
    }

    public static string Build(Guid id) => Route
        .Replace("{id}", $"{id}");
}

public record GetHlsStreamManifestQuery : IRequest<IResult>
{
    public required Guid Id { get; set; }
    public required Guid StreamSessionId { get; set; }
    public string? TranscodingAudioCodec { get; set; }
    public string? TranscodingVideoCodec { get; set; }
    public int? DefaultAudioTrackIndex { get; set; }
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
                await _context.Entry(videoMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);
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

        var hasAudioTranscoding = !string.IsNullOrWhiteSpace(query.TranscodingAudioCodec);
        var hasVideoTranscoding = !string.IsNullOrWhiteSpace(query.TranscodingVideoCodec);

        var originalVideoTrack = videoFileMetadata.VideoTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var videoCodecString = hasVideoTranscoding
            ? HlsCodecStringHelpers.GetHlsCodecs(query.TranscodingVideoCodec, audioCodec: null)
            : (originalVideoTrack != null
                ? HlsCodecStringHelpers.GetHlsCodecs(originalVideoTrack.Codec, audioCodec: null)
                : string.Empty);

        var defaultAudioTrack = videoFileMetadata.AudioTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var audioCodecString = defaultAudioTrack != null
            ? (hasAudioTranscoding
                ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, query.TranscodingAudioCodec)
                : HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, defaultAudioTrack.Codec))
            : string.Empty;

        var codecsAttribute = (videoCodecString, audioCodecString) switch
        {
            (not "", not "") => $"{videoCodecString},{audioCodecString}",
            (not "", _) => videoCodecString,
            (_, not "") => audioCodecString,
            _ => string.Empty
        };

        var audioQueryParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}"
        };

        if (!string.IsNullOrEmpty(query.TranscodingAudioCodec))
            audioQueryParams.Add($"TranscodingAudioCodec={query.TranscodingAudioCodec}");

        var audioQueryString = "?" + string.Join("&", audioQueryParams);

        var audioTracks = videoFileMetadata.AudioTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList();

        foreach (var track in audioTracks)
        {
            var isDefault = query.DefaultAudioTrackIndex.HasValue
                ? track.Index == query.DefaultAudioTrackIndex.Value
                : track == audioTracks[0];
            var trackName = !string.IsNullOrEmpty(track.Name) ? track.Name : $"Track {track.Index}";
            var language = !string.IsNullOrEmpty(track.Language) ? track.Language : "und";

            var audioUri = GetHlsAudioStreamIndexQueryUriBuilder.BuildManifestRelativePath(track.Index)
                + audioQueryString;

            playlist.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO," +
                $"GROUP-ID=\"audio\"," +
                $"NAME=\"{EscapeHlsAttribute(trackName)}\"," +
                $"LANGUAGE=\"{language}\"," +
                $"DEFAULT={BoolToYesNo(isDefault)}," +
                $"AUTOSELECT={BoolToYesNo(isDefault)}," +
                $"URI=\"{audioUri}\"");
        }

        // Generate #EXT-X-MEDIA:TYPE=SUBTITLES entries for text-based subtitle tracks
        var subtitleQueryString = $"?streamSessionId={query.StreamSessionId}";
        var textSubtitleTracks = videoFileMetadata.SubtitleTracks
            .Where(t => t.IsTextBased)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList();

        var hasSubtitles = textSubtitleTracks.Count > 0;

        foreach (var track in textSubtitleTracks)
        {
            var isDefault = track == textSubtitleTracks[0] && track.IsDefault;
            var trackName = !string.IsNullOrEmpty(track.Name) ? track.Name : $"Subtitle {track.Index}";
            var trackSlug = $"sub-{track.Index}";
            var language = !string.IsNullOrEmpty(track.Language) ? track.Language : "und";

            var subtitleUri = GetHlsSubtitleStreamIndexQueryUriBuilder.BuildManifestRelativePath(track.Index)
                + subtitleQueryString;

            playlist.AppendLine(
                $"#EXT-X-MEDIA:TYPE=SUBTITLES," +
                $"GROUP-ID=\"subs\"," +
                $"NAME=\"{EscapeHlsAttribute(trackSlug)}\"," +
                $"LANGUAGE=\"{language}\"," +
                $"DEFAULT={BoolToYesNo(isDefault)}," +
                $"AUTOSELECT={BoolToYesNo(isDefault)}," +
                $"FORCED={BoolToYesNo(track.IsForced)}," +
                $"URI=\"{subtitleUri}\"");
        }

        // Generate #EXT-X-STREAM-INF for the video variant
        var subtitlesAttribute = hasSubtitles ? ",SUBTITLES=\"subs\"" : "";

        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH={fileResolution.MaxBitrate}," +
            $"AVERAGE-BANDWIDTH={fileResolution.AverageBitrate}," +
            $"RESOLUTION={fileResolution.Width}x{fileResolution.Height}," +
            $"CODECS=\"{codecsAttribute}\"," +
            $"AUDIO=\"audio\"" +
            subtitlesAttribute);

        var playlistQuality = hasVideoTranscoding ? fileResolution.Name : "original";
        var playlistUrl = GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath(playlistQuality);

        var videoQueryParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}"
        };

        if (!string.IsNullOrEmpty(query.TranscodingVideoCodec))
            videoQueryParams.Add($"TranscodingVideoCodec={query.TranscodingVideoCodec}");

        var videoQueryString = "?" + string.Join("&", videoQueryParams);
        playlistUrl += videoQueryString;

        playlist.AppendLine(playlistUrl);
        playlist.AppendLine();

        return playlist.ToString();
    }

    private static string BoolToYesNo(bool value) => value ? "YES" : "NO";

    private static string EscapeHlsAttribute(string value) =>
        value.Replace("\"", "'");
}
