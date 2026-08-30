using System.Text;
using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

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
            { nameof(query.SubtitleBurnInStreamIndex), query.SubtitleBurnInStreamIndex?.ToString() },
            { nameof(query.Quality), query.Quality },
            { nameof(query.AudioTrackTranscodings), SerializeAudioTrackTranscodings(query.AudioTrackTranscodings) },
            { nameof(query.StartSeconds), query.StartSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { nameof(query.VideoCodecsOnly), query.VideoCodecsOnly ? "true" : null }
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

public record GetHlsStreamManifestQuery : IRequest<HttpContentResult>
{
    public required Guid Id { get; set; }
    public required Guid StreamSessionId { get; set; }
    public string? TranscodingVideoCodec { get; set; }
    public int? DefaultAudioTrackIndex { get; set; }
    public int? DefaultSubtitleTrackIndex { get; set; }
    public int? SubtitleBurnInStreamIndex { get; set; }
    public string? Quality { get; set; }
    public Dictionary<int, string>? AudioTrackTranscodings { get; set; }
    /// <summary>
    /// Optional resume offset (seconds). Propagated to media playlists as #EXT-X-START.
    /// </summary>
    public double? StartSeconds { get; set; }
    /// <summary>
    /// When true, STREAM-INF CODECS lists video only (Video.js MSE isTypeSupported).
    /// Native LibVLC needs video+audio in CODECS.
    /// </summary>
    public bool VideoCodecsOnly { get; set; }
};

public class GetHlsStreamManifestQueryHandler : IRequestHandler<GetHlsStreamManifestQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAccessGuard _accessGuard;
    private readonly IActiveStreamTracker _activeStreamTracker;
    private readonly IFfmpegCapabilitiesService _ffmpegCapabilitiesService;
    private readonly ISender _sender;
    private readonly ILogger<GetHlsStreamManifestQueryHandler> _logger;

    public GetHlsStreamManifestQueryHandler(
        IApplicationDbContext context,
        IMediaAccessGuard accessGuard,
        IActiveStreamTracker activeStreamTracker,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        ISender sender,
        ILogger<GetHlsStreamManifestQueryHandler> logger)
    {
        _context = context;
        _accessGuard = accessGuard;
        _activeStreamTracker = activeStreamTracker;
        _ffmpegCapabilitiesService = ffmpegCapabilitiesService;
        _sender = sender;
        _logger = logger;
    }

    public async Task<HttpContentResult> Handle(GetHlsStreamManifestQuery query, CancellationToken cancellationToken)
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
            return new EmptyHttpContentResult(404);
        }

        await LoadFileTracksAsync(indexedFile.FileMetadata, cancellationToken);

        var hlsSegmentsAvailable = indexedFile.FileMetadata is VideoFileMetadata
            && await HlsSegmentHelper.HasSegmentsAsync(_context, query.Id, cancellationToken);

        if (!hlsSegmentsAvailable && indexedFile.FileMetadata is VideoFileMetadata)
        {
            await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(
                _sender,
                query.Id,
                _logger,
                cancellationToken);
        }

        if (query.SubtitleBurnInStreamIndex is int burnInIndex
            && indexedFile.FileMetadata is VideoFileMetadata videoMetadata)
        {
            var burnInTrack = videoMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == burnInIndex);
            if (burnInTrack is not null)
            {
                var existing = _activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
                _activeStreamTracker.UpdateStreamDecision(
                    query.StreamSessionId,
                    StreamDecisionExtensions.ApplySubtitleBurnIn(existing, burnInTrack));
            }
        }

        if (indexedFile.FileMetadata is VideoFileMetadata videoMetadataForQuality)
            ApplyQualityDownscaleIfRequested(videoMetadataForQuality, query);

        if (indexedFile.FileMetadata is VideoFileMetadata)
        {
            await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(
                query.StreamSessionId,
                _activeStreamTracker,
                _ffmpegCapabilitiesService,
                cancellationToken);
        }

        var masterPlaylist = indexedFile.FileMetadata switch
        {
            AudioFileMetadata x => GenerateAudioFileMasterPlaylist(x, query),
            VideoFileMetadata x => GenerateVideoFileMasterPlaylist(x, query, hlsSegmentsAvailable),
            _ => throw new InvalidOperationException(
                $"Indexed file has unsupported metadata type '{indexedFile.FileMetadata?.GetType().Name ?? "null"}'.")
        };
        return new TextHttpContentResult(masterPlaylist, "application/vnd.apple.mpegurl");
    }

    private void ApplyQualityDownscaleIfRequested(
        VideoFileMetadata videoMetadata,
        GetHlsStreamManifestQuery query)
    {
        if (string.IsNullOrEmpty(query.Quality) || query.Quality == "original")
            return;

        var fileResolution = Constants.VideoQualities.Single(x => x.Key == videoMetadata.VideoResolution).Value;
        var requestedQuality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.Quality);
        // Same height as source is a ladder encode (bitrate-capped), not remux.
        if (requestedQuality.Value is null || requestedQuality.Value.Height > fileResolution.Height)
            return;

        var effectiveVideoCodec = query.TranscodingVideoCodec ?? "h264";

        var originalVideoTrack = videoMetadata.VideoTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var sourceResolution = originalVideoTrack is { Width: > 0, Height: > 0 }
            ? $"{originalVideoTrack.Width}x{originalVideoTrack.Height}"
            : $"{fileResolution.Width}x{fileResolution.Height}";

        var existing = _activeStreamTracker.GetStreamInfo(query.StreamSessionId)?.StreamDecision;
        _activeStreamTracker.UpdateStreamDecision(
            query.StreamSessionId,
            StreamDecisionExtensions.ApplyQualityDownscale(
                existing,
                requestedQuality.Value,
                effectiveVideoCodec,
                sourceResolution));
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

        AppendStartSeconds(trackAudioParams, query.StartSeconds);

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

    private static string GenerateVideoFileMasterPlaylist(
        VideoFileMetadata videoFileMetadata,
        GetHlsStreamManifestQuery query,
        bool hlsSegmentsAvailable)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");
        if (query.StartSeconds is > 0)
        {
            playlist.AppendLine(
                "#EXT-X-START:TIME-OFFSET="
                + query.StartSeconds.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ",PRECISE=NO");
        }

        var fileResolutionIdentifier = videoFileMetadata.VideoResolution;
        var fileResolution = Constants.VideoQualities.Single(x => x.Key == fileResolutionIdentifier).Value;

        // Copy so Video.js can force AAC without mutating the request DTO.
        var audioTrackTranscodings = query.AudioTrackTranscodings is { Count: > 0 }
            ? new Dictionary<int, string>(query.AudioTrackTranscodings)
            : new Dictionary<int, string>();

        // Video.js / MSE cannot remux EAC3/DTS into fMP4. ffmpeg -f segment also hangs on
        // those remuxes (stuck empty_moov init.m4s ~453 bytes). Force AAC for unreliable
        // bitstream-copy codecs even when the device can decode them in Direct Play.
        // VideoCodecsOnly (Windows Video.js) forces AAC for every non-AAC track.
        foreach (var track in videoFileMetadata.AudioTracks)
        {
            if (audioTrackTranscodings.ContainsKey(track.Index))
                continue;

            if (query.VideoCodecsOnly)
            {
                if (MediaCodecNames.EqualsCodec(track.Codec, "aac"))
                    continue;

                audioTrackTranscodings[track.Index] = "aac";
                continue;
            }

            if (IsUnreliableHlsFmp4AudioRemux(track.Codec))
                audioTrackTranscodings[track.Index] = "aac";
        }

        var hasVideoTranscoding = !string.IsNullOrWhiteSpace(query.TranscodingVideoCodec);

        var originalVideoTrack = videoFileMetadata.VideoTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var videoCodecString = hasVideoTranscoding
            ? HlsCodecStringHelpers.GetHlsCodecs(query.TranscodingVideoCodec, audioCodec: null)
            : HlsCodecStringHelpers.GetHlsVideoCodecString(originalVideoTrack);

        var defaultAudioTrack = videoFileMetadata.AudioTracks
            .OrderByDescending(t => query.DefaultAudioTrackIndex is int want && t.Index == want)
            .ThenByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .FirstOrDefault();

        var defaultAudioNeedsTranscoding = defaultAudioTrack != null
            && audioTrackTranscodings.ContainsKey(defaultAudioTrack.Index);

        var audioCodecString = defaultAudioTrack != null
            ? (defaultAudioNeedsTranscoding
                ? HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, audioTrackTranscodings[defaultAudioTrack.Index])
                : HlsCodecStringHelpers.GetHlsCodecs(videoCodec: null, defaultAudioTrack.Codec))
            : string.Empty;

        var audioTracks = OrderHlsAudioTracks(
            videoFileMetadata.AudioTracks,
            query.DefaultAudioTrackIndex);

        var usedAudioNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in audioTracks)
        {
            var isDefault = query.DefaultAudioTrackIndex.HasValue
                ? track.Index == query.DefaultAudioTrackIndex.Value
                : track == audioTracks[0];
            var trackName = AudioTrackDisplayHelper.FormatHlsName(track.Name, track.Language, track.Index, usedAudioNames);
            var language = !string.IsNullOrEmpty(track.Language) ? track.Language : "und";
            var channels = track.Channels > 0 ? track.Channels : 2;

            var trackAudioParams = new List<string>
                {
                    $"streamSessionId={query.StreamSessionId}"
                };

            if (audioTrackTranscodings.TryGetValue(track.Index, out var transcodingCodec))
                trackAudioParams.Add($"TranscodingAudioCodec={transcodingCodec}");

            AppendStartSeconds(trackAudioParams, query.StartSeconds);

            var audioQueryString = "?" + string.Join("&", trackAudioParams);

            var audioUri = GetHlsAudioStreamIndexQueryUriBuilder.BuildManifestRelativePath(track.Index)
                + audioQueryString;

            // CHANNELS helps LibVLC adaptive join demuxed audio more reliably.
            playlist.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO," +
                $"GROUP-ID=\"audio\"," +
                $"NAME=\"{EscapeHlsAttribute(trackName)}\"," +
                $"LANGUAGE=\"{language}\"," +
                $"DEFAULT={BoolToYesNo(isDefault)}," +
                $"AUTOSELECT={BoolToYesNo(isDefault)}," +
                $"CHANNELS=\"{channels}\"," +
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
            if (requestedQuality.Value is not null && requestedQuality.Value.Height <= fileResolution.Height)
            {
                targetResolution = requestedQuality.Value;
                playlistQuality = requestedQuality.Value.Name;
                // Ladder quality (same or lower height) requires transcoding - force h264 if unset
                effectiveVideoCodec ??= "h264";
            }
        }

        // Bitmap subtitles (PGS) are burned into the video and require transcoding
        if (query.SubtitleBurnInStreamIndex.HasValue)
        {
            effectiveVideoCodec ??= query.TranscodingVideoCodec ?? HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        // Transmuxing requires keyframe-based segments; fall back to transcoding when they are missing
        if (!hlsSegmentsAvailable && string.IsNullOrEmpty(effectiveVideoCodec))
        {
            effectiveVideoCodec = HlsSegmentHelper.FallbackTranscodingVideoCodec;
        }

        var effectiveVideoCodecString = !string.IsNullOrEmpty(effectiveVideoCodec)
            ? HlsCodecStringHelpers.GetHlsCodecs(effectiveVideoCodec, audioCodec: null)
            : videoCodecString;

        // LibVLC: STREAM-INF CODECS lists video+audio even with a demuxed AUDIO
        // group. Video.js MSE rejects combined types - Web passes VideoCodecsOnly.
        string effectiveCodecsAttribute;
        if (query.VideoCodecsOnly)
        {
            effectiveCodecsAttribute = !string.IsNullOrEmpty(effectiveVideoCodecString)
                ? effectiveVideoCodecString
                : audioCodecString;
        }
        else if (!string.IsNullOrEmpty(effectiveVideoCodecString)
            && !string.IsNullOrEmpty(audioCodecString))
        {
            effectiveCodecsAttribute = $"{effectiveVideoCodecString},{audioCodecString}";
        }
        else
        {
            effectiveCodecsAttribute = !string.IsNullOrEmpty(effectiveVideoCodecString)
                ? effectiveVideoCodecString
                : audioCodecString;
        }

        playlist.AppendLine($"#EXT-X-STREAM-INF:" +
            $"BANDWIDTH={targetResolution.MaxBitrate}," +
            $"AVERAGE-BANDWIDTH={targetResolution.AverageBitrate}," +
            $"RESOLUTION={targetResolution.Width}x{targetResolution.Height}," +
            $"CODECS=\"{effectiveCodecsAttribute}\"" +
            ",AUDIO=\"audio\"" +
            ",CLOSED-CAPTIONS=NONE" +
            subtitlesAttribute);

        var playlistUrl = GetHlsVideoStreamIndexQueryUriBuilder.BuildManifestRelativePath(playlistQuality);

        var videoQueryParams = new List<string>
        {
            $"streamSessionId={query.StreamSessionId}"
        };

        if (!string.IsNullOrEmpty(effectiveVideoCodec))
            videoQueryParams.Add($"TranscodingVideoCodec={effectiveVideoCodec}");

        if (query.SubtitleBurnInStreamIndex.HasValue)
            videoQueryParams.Add($"SubtitleBurnInStreamIndex={query.SubtitleBurnInStreamIndex.Value}");

        AppendStartSeconds(videoQueryParams, query.StartSeconds);

        var videoQueryString = "?" + string.Join("&", videoQueryParams);
        playlistUrl += videoQueryString;

        playlist.AppendLine(playlistUrl);
        playlist.AppendLine();

        return playlist.ToString();
    }

    private static List<AudioFileTrack> OrderHlsAudioTracks(
        IEnumerable<AudioFileTrack> tracks,
        int? selectedIndex) =>
        tracks
            .OrderByDescending(t => selectedIndex is int want && t.Index == want)
            .ThenByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList();

    /// <summary>
    /// Bitstream-copy of these codecs into <c>-f segment</c> fMP4 often stalls on a
    /// size-zero empty_moov init (~453 bytes) until the client times out with 503.
    /// </summary>
    private static bool IsUnreliableHlsFmp4AudioRemux(string? codec)
    {
        var canonical = MediaCodecNames.Canonical(codec);
        return canonical is "ac3" or "eac3" or "dts" or "truehd" or "dtshd" or "mlp";
    }

    private static string BoolToYesNo(bool value) => value ? "YES" : "NO";

    private static string EscapeHlsAttribute(string value) =>
        value.Replace("\"", "'");

    private static void AppendStartSeconds(List<string> queryParams, double? startSeconds)
    {
        if (startSeconds is > 0)
        {
            queryParams.Add(
                $"startSeconds={startSeconds.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
        }
    }
}
