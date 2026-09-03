using System.Globalization;
using System.Text;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Labels for the admin playback HUD. Keep strings ASCII so the overlay stays
/// consistent with native chrome comments/docs.
/// </summary>
public static class NativePlaybackStatsFormatting
{
    public static string PlayMethod(string? url, string? mimeType, bool isOriginal)
    {
        if (LocalPlaybackUrl.IsLocalFile(url))
            return "Offline";

        if (StreamingSourceKind.IsHls(mimeType, url))
            return isOriginal ? "Remux HLS" : "Transcode HLS";

        if (url is not null && url.Contains("/direct-stream", StringComparison.OrdinalIgnoreCase))
            return "Direct Play";

        return "Stream";
    }

    public static string ShortCodec(string? mimeOrCodec)
    {
        if (string.IsNullOrWhiteSpace(mimeOrCodec))
            return "?";

        var value = mimeOrCodec.Trim();
        var slash = value.LastIndexOf('/');
        if (slash >= 0 && slash < value.Length - 1)
            value = value[(slash + 1)..];

        var plus = value.IndexOf('+');
        if (plus > 0)
            value = value[..plus];

        return value.ToUpperInvariant() switch
        {
            "DOLBY-VISION" or "DOLBY_VISION" => "DV",
            "HEVC" or "H265" or "H.265" => "HEVC",
            "AVC" or "H264" or "H.264" => "AVC",
            "AV01" or "AV1" => "AV1",
            "VP9" => "VP9",
            "EAC3" or "EC-3" => "EAC3",
            "AC3" or "AC-3" => "AC3",
            "TRUEHD" => "TrueHD",
            "DTS" => "DTS",
            "MP4A-LATM" or "MP4A" or "AAC" => "AAC",
            _ => value.ToUpperInvariant()
        };
    }

    public static string VideoLine(string? codec, int width, int height, float fps, int bitrateBps)
    {
        var parts = new List<string> { ShortCodec(codec) };
        if (width > 0 && height > 0)
            parts.Add($"{width}x{height}");
        if (fps > 1f && fps < 125f)
            parts.Add(fps.ToString("0.###", CultureInfo.InvariantCulture) + " fps");
        if (bitrateBps > 0)
            parts.Add(FormatBitrate(bitrateBps));
        return string.Join("  ", parts);
    }

    public static string AudioLine(string? codec, int channels, int sampleRateHz, int bitrateBps)
    {
        var parts = new List<string> { ShortCodec(codec) };
        if (channels > 0)
            parts.Add(channels + "ch");
        if (sampleRateHz > 0)
            parts.Add(FormatSampleRate(sampleRateHz));
        if (bitrateBps > 0)
            parts.Add(FormatBitrate(bitrateBps));
        return string.Join("  ", parts);
    }

    public static string FormatBitrate(int bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
            return "";

        var mbps = bitsPerSecond / 1_000_000.0;
        if (mbps >= 1)
            return mbps.ToString("0.0", CultureInfo.InvariantCulture) + " Mbps";

        var kbps = bitsPerSecond / 1000.0;
        return kbps.ToString("0", CultureInfo.InvariantCulture) + " kbps";
    }

    public static string FormatBuffer(double bufferedSeconds)
    {
        if (bufferedSeconds < 0)
            return "";

        return "buf " + bufferedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }

    public static string FormatFrames(int dropped, int rendered, int skipped)
    {
        return "drop " + dropped + " / draw " + rendered + "  skip " + skipped;
    }

    public static string FormatHdmiModes(IReadOnlyList<HdmiDisplayMode> modes)
    {
        if (modes.Count == 0)
            return "";

        var groups = modes
            .Where(m => m.Width > 0 && m.Height > 0 && m.Hz > 1f)
            .GroupBy(m => m.Height)
            .OrderByDescending(g => g.Key);

        var parts = new List<string>();
        foreach (var group in groups)
        {
            var rates = group
                .GroupBy(m => m.Hz.ToString("0.##", CultureInfo.InvariantCulture))
                .Select(g => g.Any(m => m.IsCurrent) ? g.First(m => m.IsCurrent) : g.First())
                .OrderByDescending(m => m.Hz)
                .Select(FormatHdmiRate);
            parts.Add(group.Key + "p: " + string.Join(" ", rates));
        }

        return string.Join(" | ", parts);
    }

    public static NativePlaybackStatsSnapshot WithDecision(
        NativePlaybackStatsSnapshot runtime,
        StreamDecisionDto? decision,
        StreamDecisionHudLabels? labels = null)
    {
        var loc = labels ?? StreamDecisionPlayback.CurrentLabels();
        if (decision is null)
            return runtime;

        return new NativePlaybackStatsSnapshot
        {
            Mode = StreamDecisionPlayback.OverallMode(decision, loc),
            PlayMethod = runtime.PlayMethod,
            Quality = runtime.Quality,
            VideoDecision = FormatVideoDecision(decision, loc),
            AudioDecision = FormatAudioDecision(decision, loc),
            SubtitleDecision = FormatSubtitleDecision(decision, loc),
            Reason = StreamDecisionPlayback.FormatReason(decision.Reason, loc),
            StreamResolution = StreamDecisionPlayback.FormatResolution(decision),
            Encoder = FormatEncoder(decision, loc),
            Video = runtime.Video,
            Audio = runtime.Audio,
            VideoDecoder = runtime.VideoDecoder,
            AudioDecoder = runtime.AudioDecoder,
            Hdmi = runtime.Hdmi,
            HdmiModes = runtime.HdmiModes,
            Cadence = runtime.Cadence,
            CadenceWarning = runtime.CadenceWarning,
            Frames = runtime.Frames,
            Buffer = runtime.Buffer,
            Bandwidth = runtime.Bandwidth,
            Policy = runtime.Policy
        };
    }

    public static string ToHudText(NativePlaybackStatsSnapshot snapshot)
    {
        var sb = new StringBuilder();
        AppendLine(sb, HeaderLine(snapshot));
        AppendLine(sb, snapshot.VideoDecision);
        AppendLine(sb, snapshot.AudioDecision);
        AppendLine(sb, snapshot.SubtitleDecision);
        AppendLine(sb, snapshot.StreamResolution);
        AppendLine(sb, snapshot.Reason);
        AppendLine(sb, snapshot.Encoder);
        AppendLine(sb, snapshot.Video);
        AppendLine(sb, snapshot.Audio);
        AppendLine(sb, Prefix("V  ", snapshot.VideoDecoder));
        AppendLine(sb, Prefix("A  ", snapshot.AudioDecoder));
        AppendLine(sb, Join("  ", snapshot.Hdmi, snapshot.Cadence));
        AppendLine(sb, snapshot.HdmiModes);
        AppendLine(sb, snapshot.Frames);
        AppendLine(sb, Join("  ", snapshot.Buffer, snapshot.Bandwidth));
        AppendLine(sb, DotJoin(snapshot.Policy));
        return sb.ToString().TrimEnd();
    }

    public static string Headline(NativePlaybackStatsSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.Mode) ? snapshot.PlayMethod : snapshot.Mode;

    public static string HeaderLine(NativePlaybackStatsSnapshot snapshot)
    {
        var mode = Headline(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.Quality))
            return mode;
        if (string.IsNullOrWhiteSpace(mode))
            return snapshot.Quality;
        return mode + "  " + snapshot.Quality;
    }

    public static string DecisionBlock(NativePlaybackStatsSnapshot snapshot)
    {
        var sb = new StringBuilder();
        AppendLine(sb, snapshot.VideoDecision);
        AppendLine(sb, snapshot.AudioDecision);
        AppendLine(sb, snapshot.SubtitleDecision);
        AppendLine(sb, snapshot.StreamResolution);
        AppendLine(sb, snapshot.Reason);
        AppendLine(sb, snapshot.Encoder);
        return sb.ToString().TrimEnd();
    }

    public static string RuntimeBlock(NativePlaybackStatsSnapshot snapshot)
    {
        var sb = new StringBuilder();
        AppendLine(sb, snapshot.Video);
        AppendLine(sb, snapshot.Audio);
        AppendLine(sb, Prefix("V  ", snapshot.VideoDecoder));
        AppendLine(sb, Prefix("A  ", snapshot.AudioDecoder));
        AppendLine(sb, Join("  ", snapshot.Hdmi, snapshot.Cadence));
        AppendLine(sb, snapshot.HdmiModes);
        AppendLine(sb, snapshot.Frames);
        AppendLine(sb, Join("  ", snapshot.Buffer, snapshot.Bandwidth));
        return sb.ToString().TrimEnd();
    }

    public static string PolicyBlock(NativePlaybackStatsSnapshot snapshot) =>
        DotJoin(snapshot.Policy);

    private static string FormatVideoDecision(StreamDecisionDto decision, StreamDecisionHudLabels labels)
    {
        if (decision.SourceVideoCodec is null)
            return "";

        var from = ShortCodec(decision.SourceVideoCodec);
        var to = ShortCodec(decision.StreamVideoCodec ?? decision.SourceVideoCodec);
        var verb = StreamDecisionPlayback.IsVideoTranscoded(decision) ? labels.Transcode : labels.Direct;
        return "V  " + from + " -> " + to + "  " + verb;
    }

    private static string FormatAudioDecision(StreamDecisionDto decision, StreamDecisionHudLabels labels)
    {
        if (decision.SourceAudioCodec is null)
            return "";

        var from = ShortCodec(decision.SourceAudioCodec);
        var to = ShortCodec(decision.StreamAudioCodec ?? decision.SourceAudioCodec);
        var verb = StreamDecisionPlayback.IsAudioTranscoded(decision) ? labels.Transcode : labels.Direct;
        var lang = decision.AudioTrackLanguage;
        var line = "A  " + from + " -> " + to + "  " + verb;
        if (!string.IsNullOrWhiteSpace(lang))
            line += "  " + lang.ToUpperInvariant();
        return line;
    }

    private static string FormatSubtitleDecision(StreamDecisionDto decision, StreamDecisionHudLabels labels)
    {
        if (!StreamDecisionPlayback.HasSubtitleTrack(decision))
            return "";

        var verb = StreamDecisionPlayback.IsSubtitleBurnIn(decision) ? labels.BurnIn : labels.Sidecar;
        var parts = new List<string> { "S  " + verb };
        if (!string.IsNullOrWhiteSpace(decision.SubtitleTrackLanguage))
            parts.Add(decision.SubtitleTrackLanguage.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(decision.SubtitleCodec))
            parts.Add(ShortCodec(decision.SubtitleCodec));
        return string.Join("  ", parts);
    }

    private static string FormatEncoder(StreamDecisionDto decision, StreamDecisionHudLabels labels)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(decision.VideoEncoder))
        {
            var kind = decision.IsHardwareAccelerated == true ? labels.Hardware : labels.Software;
            parts.Add(decision.VideoEncoder + " (" + kind + ")");
        }

        if (!string.IsNullOrWhiteSpace(decision.AudioEncoder))
            parts.Add(decision.AudioEncoder + " (" + labels.Software + ")");

        return string.Join("  ", parts);
    }

    private static string DotJoin(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
            return "";

        return string.Join(
            " | ",
            policy.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string FormatHdmiRate(HdmiDisplayMode mode)
    {
        var hz = mode.Hz.ToString("0.##", CultureInfo.InvariantCulture);
        return mode.IsCurrent ? hz + "*" : hz;
    }

    private static string FormatSampleRate(int hz)
    {
        if (hz >= 1000 && hz % 1000 == 0)
            return (hz / 1000).ToString(CultureInfo.InvariantCulture) + " kHz";
        if (hz >= 1000)
            return (hz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " kHz";
        return hz.ToString(CultureInfo.InvariantCulture) + " Hz";
    }

    private static void AppendLine(StringBuilder sb, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        sb.AppendLine(line);
    }

    private static string Prefix(string prefix, string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : prefix + value;

    private static string Join(string separator, params string?[] parts)
    {
        var filled = parts.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(separator, filled);
    }
}

public readonly record struct HdmiDisplayMode(int Width, int Height, float Hz, bool IsCurrent);

public sealed class NativePlaybackStatsSnapshot
{
    public string Mode { get; init; } = "";
    public string PlayMethod { get; init; } = "";
    public string Quality { get; init; } = "";
    public string VideoDecision { get; init; } = "";
    public string AudioDecision { get; init; } = "";
    public string SubtitleDecision { get; init; } = "";
    public string Reason { get; init; } = "";
    public string StreamResolution { get; init; } = "";
    public string Encoder { get; init; } = "";
    public string Video { get; init; } = "";
    public string Audio { get; init; } = "";
    public string VideoDecoder { get; init; } = "";
    public string AudioDecoder { get; init; } = "";
    public string Hdmi { get; init; } = "";
    public string HdmiModes { get; init; } = "";
    public string Cadence { get; init; } = "";
    public bool CadenceWarning { get; init; }
    public string Frames { get; init; } = "";
    public string Buffer { get; init; } = "";
    public string Bandwidth { get; init; } = "";
    public string Policy { get; init; } = "";
}
