using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Application.Services;
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
            { nameof(query.TranscodingVideoCodec), query.TranscodingVideoCodec },
            { nameof(query.DefaultAudioTrackIndex), query.DefaultAudioTrackIndex?.ToString() },
            { nameof(query.DefaultSubtitleTrackIndex), query.DefaultSubtitleTrackIndex?.ToString() },
            { nameof(query.Quality), query.Quality },
            { nameof(query.AudioTrackTranscodings), SerializeAudioTrackTranscodings(query.AudioTrackTranscodings) }
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

    private static string? SerializeAudioTrackTranscodings(Dictionary<int, string>? map)
    {
        if (map is not { Count: > 0 })
            return null;

        return string.Join(",", map.Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    public static Dictionary<int, string>? DeserializeAudioTrackTranscodings(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var result = new Dictionary<int, string>();
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var trackIndex))
            {
                result[trackIndex] = parts[1];
            }
        }

        return result.Count > 0 ? result : null;
    }
}

public record GetHlsStreamManifestQuery : IRequest<IResult>
{
    public required Guid Id { get; set; }
    public required Guid StreamSessionId { get; set; }
    public string? TranscodingVideoCodec { get; set; }
    public int? DefaultAudioTrackIndex { get; set; }
    public int? DefaultSubtitleTrackIndex { get; set; }
    public string? Quality { get; set; }
    public Dictionary<int, string>? AudioTrackTranscodings { get; set; }
};

public class GetHlsStreamManifestQueryHandler : IRequestHandler<GetHlsStreamManifestQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAccessGuard _accessGuard;

    public GetHlsStreamManifestQueryHandler(IApplicationDbContext context, IMediaAccessGuard accessGuard)
    {
        _context = context;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> Handle(GetHlsStreamManifestQuery query, CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureAccessByIndexedFileAsync(query.Id, cancellationToken);

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
            AudioFileMetadata x => GenerateAudioFileMasterPlaylist(x, query),
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

    private static string GenerateAudioFileMasterPlaylist(AudioFileMetadata audioFileMetadata, GetHlsStreamManifestQuery query)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");

        var audioTrack = audioFileMetadata.AudioTrack;
        var audioTrackIndex = audioTrack?.Index ?? 0;

        var audioTrackTranscodings = query.AudioTrackTranscodings ?? [];
        var needsTranscoding = audioTrackTranscodings.TryGetValue(audioTrackIndex, out var transcodingCodec);

        var codecString = needsTranscoding
            ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, transcodingCodec)
            : (audioTrack != null
                ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioTrack.Codec)
                : string.Empty);

        var trackAudioParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}"
        };

        if (needsTranscoding)
            trackAudioParams.Add($"TranscodingAudioCodec={transcodingCodec}");

        var audioQueryString = "?" + string.Join("&", trackAudioParams);

        var audioUri = GetHlsAudioStreamIndexQueryUriBuilder.BuildManifestRelativePath(audioTrackIndex)
            + audioQueryString;

        playlist.AppendLine(
            $"#EXT-X-MEDIA:TYPE=AUDIO," +
            $"GROUP-ID=\"audio\"," +
            $"NAME=\"default\"," +
            $"LANGUAGE=\"und\"," +
            $"DEFAULT=YES," +
            $"AUTOSELECT=YES," +
            $"URI=\"{audioUri}\"");

        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH=256000," +
            (string.IsNullOrEmpty(codecString) ? "" : $"CODECS=\"{codecString}\",") +
            $"AUDIO=\"audio\"");

        playlist.AppendLine(audioUri);
        playlist.AppendLine();

        return playlist.ToString();
    }

    private static string GenerateVideoFileMasterPlaylist(VideoFileMetadata videoFileMetadata, GetHlsStreamManifestQuery query)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");

        var fileResolutionIdentifier = videoFileMetadata.VideoResolution;
        var fileResolution = Constants.VideoQualities.Single(x => x.Key == fileResolutionIdentifier).Value;

        var audioTrackTranscodings = query.AudioTrackTranscodings ?? [];
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

        var defaultAudioNeedsTranscoding = defaultAudioTrack != null
            && audioTrackTranscodings.ContainsKey(defaultAudioTrack.Index);

        var audioCodecString = defaultAudioTrack != null
            ? (defaultAudioNeedsTranscoding
                ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioTrackTranscodings[defaultAudioTrack.Index])
                : HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, defaultAudioTrack.Codec))
            : string.Empty;

        var codecsAttribute = (videoCodecString, audioCodecString) switch
        {
            (not "", not "") => $"{videoCodecString},{audioCodecString}",
            (not "", _) => videoCodecString,
            (_, not "") => audioCodecString,
            _ => string.Empty
        };

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

                var trackAudioParams = new List<string>
                {
                    $"streamSessionId={query.StreamSessionId}"
                };

                if (audioTrackTranscodings.TryGetValue(track.Index, out var transcodingCodec))
                    trackAudioParams.Add($"TranscodingAudioCodec={transcodingCodec}");

                var audioQueryString = "?" + string.Join("&", trackAudioParams);

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
            var isDefault = query.DefaultSubtitleTrackIndex is { } defaultSubIdx
                ? track.Index == defaultSubIdx
                : track == textSubtitleTracks[0] && track.IsDefault;
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

        // Determine the target resolution (requested quality or source quality)
        var targetResolution = fileResolution;
        var playlistQuality = "original";
        var effectiveVideoCodec = query.TranscodingVideoCodec;

        if (!string.IsNullOrEmpty(query.Quality) && query.Quality != "original")
        {
            var requestedQuality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
            if (requestedQuality.Value is not null && requestedQuality.Value.Height < fileResolution.Height)
            {
                targetResolution = requestedQuality.Value;
                playlistQuality = requestedQuality.Value.Name;
                // Downscaling requires transcoding - force h264 if no codec was specified
                effectiveVideoCodec ??= "h264";
            }
        }

        var effectiveVideoCodecString = !string.IsNullOrEmpty(effectiveVideoCodec)
            ? HlsCodecStringHelpers.GetHlsCodecs(effectiveVideoCodec, audioCodec: null)
            : videoCodecString;

        var effectiveCodecsAttribute = (effectiveVideoCodecString, audioCodecString) switch
        {
            (not "", not "") => $"{effectiveVideoCodecString},{audioCodecString}",
            (not "", _) => effectiveVideoCodecString,
            (_, not "") => audioCodecString,
            _ => string.Empty
        };

        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH={targetResolution.MaxBitrate}," +
            $"AVERAGE-BANDWIDTH={targetResolution.AverageBitrate}," +
            $"RESOLUTION={targetResolution.Width}x{targetResolution.Height}," +
            $"CODECS=\"{effectiveCodecsAttribute}\"" +
            ",AUDIO=\"audio\"" +
            subtitlesAttribute);

        var playlistUrl = GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath(playlistQuality);

        var videoQueryParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}"
        };

        if (!string.IsNullOrEmpty(effectiveVideoCodec))
            videoQueryParams.Add($"TranscodingVideoCodec={effectiveVideoCodec}");

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
