using System.Globalization;
using K7.Server.Domain.Common;
using K7.Shared.Dtos.Devices;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Human-readable labels for Settings -> About codec probes.
/// </summary>
public static class DeviceCodecDisplay
{
    public static IReadOnlyList<string> FormatContainers(DeviceCodecSummaryDto summary) =>
        DistinctSorted(summary.Containers.Select(PrettyContainer));

    public static IReadOnlyList<string> FormatAudio(DeviceCodecSummaryDto summary) =>
        DistinctSorted(summary.AudioCodecs.Select(PrettyAudio));

    public static IReadOnlyList<string> FormatSubtitles(DeviceCodecSummaryDto summary) =>
        DistinctSorted(summary.SubtitleCodecs.Select(PrettySubtitle));

    public static IReadOnlyList<string> FormatVideo(DeviceCodecSummaryDto summary)
    {
        var profiles = summary.VideoProfiles ?? [];
        var labels = new List<string>();

        foreach (var codec in summary.VideoCodecs.Order(StringComparer.OrdinalIgnoreCase))
        {
            var canonical = codec.Trim();
            if (canonical.Equals("hevc", StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("h265", StringComparison.OrdinalIgnoreCase))
            {
                labels.AddRange(FormatProfiledCodec("HEVC", "hevc", profiles));
                continue;
            }

            if (canonical.Equals("av1", StringComparison.OrdinalIgnoreCase))
            {
                labels.AddRange(FormatProfiledCodec("AV1", "av1", profiles));
                continue;
            }

            labels.Add(PrettyVideo(canonical));
        }

        return DistinctSorted(labels);
    }

    private static IEnumerable<string> FormatProfiledCodec(string family, string tokenCodec, IEnumerable<string> profiles)
    {
        var ids = profiles as ICollection<string> ?? profiles.ToList();
        var variants = new List<string>();
        if (ids.Contains(VideoDecoderProfileTokens.Prefix + tokenCodec + ":main"))
            variants.Add(family + " Main");
        if (ids.Contains(VideoDecoderProfileTokens.Prefix + tokenCodec + ":main10"))
            variants.Add(family + " Main 10");
        if (tokenCodec is "hevc" && ids.Contains(VideoDecoderProfileTokens.HevcDolbyVision))
            variants.Add(family + " Dolby Vision");

        if (variants.Count == 0)
            variants.Add(family);

        var suffix = FormatLimits(tokenCodec, ids);
        return variants.Select(v => v + suffix);
    }

    private static string FormatLimits(string codec, IEnumerable<string> ids)
    {
        var levelPrefix = VideoDecoderProfileTokens.Prefix + codec + VideoDecoderProfileTokens.LevelInfix;
        var maxPrefix = VideoDecoderProfileTokens.Prefix + codec + VideoDecoderProfileTokens.MaxInfix;
        var level = 0;
        var max = "";

        foreach (var id in ids)
        {
            if (id.StartsWith(levelPrefix, StringComparison.Ordinal)
                && int.TryParse(id.AsSpan(levelPrefix.Length), out var parsed)
                && parsed > level)
            {
                level = parsed;
            }

            if (id.StartsWith(maxPrefix, StringComparison.Ordinal))
                max = id[maxPrefix.Length..];
        }

        var parts = new List<string>();
        if (level > 0)
            parts.Add("L" + FormatHevcLevel(codec, level));
        if (!string.IsNullOrEmpty(max))
            parts.Add(max);

        return parts.Count == 0
            ? ""
            : " (" + string.Join(", ", parts) + ")";
    }

    private static string FormatHevcLevel(string codec, int level)
    {
        if (codec is not "hevc")
            return level.ToString(CultureInfo.InvariantCulture);

        var idc = level < 30 ? level * 30 : level;
        return (idc / 30d).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string PrettyVideo(string codec) => codec.ToLowerInvariant() switch
    {
        "h264" or "avc" => "H.264",
        "h265" or "hevc" => "HEVC",
        "mpeg2" or "mpeg2video" => "MPEG-2",
        "mpeg4" => "MPEG-4",
        "vp8" => "VP8",
        "vp9" => "VP9",
        "av1" => "AV1",
        "theora" => "Theora",
        _ => codec.ToUpperInvariant()
    };

    private static string PrettyAudio(string codec) => codec.ToLowerInvariant() switch
    {
        "aac" => "AAC",
        "aache" or "aac-he" => "AAC-HE",
        "mp3" => "MP3",
        "mp2" => "MP2",
        "opus" => "Opus",
        "vorbis" => "Vorbis",
        "flac" => "FLAC",
        "alac" => "ALAC",
        "ac3" => "AC3",
        "eac3" => "EAC3",
        "dts" => "DTS",
        "truehd" => "TrueHD",
        "pcm" => "PCM",
        "wma" => "WMA",
        "oggaudio" => "Ogg",
        "m4a" => "M4A",
        "wav" => "WAV",
        _ => codec.ToUpperInvariant()
    };

    private static string PrettyContainer(string container) => container.ToLowerInvariant() switch
    {
        "mkv" or "matroska" => "MKV",
        "mp4" => "MP4",
        "webm" => "WebM",
        "ts" or "mpegts" => "MPEG-TS",
        "ogg" => "Ogg",
        "avi" => "AVI",
        "mov" => "MOV",
        "m4v" => "M4V",
        "m4a" => "M4A",
        "mp3" => "MP3",
        "flac" => "FLAC",
        "wav" => "WAV",
        "aac" => "AAC",
        "3gp" => "3GP",
        "mpeg" => "MPEG",
        "asf" => "ASF",
        "flv" => "FLV",
        _ => container.ToUpperInvariant()
    };

    private static string PrettySubtitle(string codec) => codec.ToLowerInvariant() switch
    {
        "webvtt" or "vtt" => "WebVTT",
        "subrip" or "srt" => "SRT",
        "ass" => "ASS",
        "pgs" => "PGS",
        _ => codec.ToUpperInvariant()
    };

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
