using System.Globalization;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Stream decision labels and client-side alignment for the admin playback HUD.
/// Same classification as the admin dashboard stream card.
/// </summary>
public static class StreamDecisionPlayback
{
    public static bool IsSubtitleBurnIn(StreamDecisionDto? decision) =>
        decision is { IsSubtitleBurnIn: true }
        || decision?.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn) == true;

    public static bool HasSubtitleTrack(StreamDecisionDto? decision) =>
        decision is { } d
        && (IsSubtitleBurnIn(d)
            || d.SubtitleTrackLanguage is not null
            || d.SubtitleTrackTitle is not null
            || d.SubtitleCodec is not null);

    public static bool IsVideoTranscoded(StreamDecisionDto? decision) =>
        decision is { } d
        && (d.Mode == PlaybackMode.Transcode
            || IsSubtitleBurnIn(d)
            || d.Reason.HasFlag(TranscodeReason.ResolutionNotSupported)
            || d.Reason.HasFlag(TranscodeReason.QualityDownscale)
            || HasResolutionDownscale(d)
            || (d.SourceVideoCodec is not null
                && d.StreamVideoCodec is not null
                && !string.Equals(d.SourceVideoCodec, d.StreamVideoCodec, StringComparison.OrdinalIgnoreCase)));

    public static bool IsAudioTranscoded(StreamDecisionDto? decision) =>
        decision is { } d
        && d.SourceAudioCodec is not null
        && d.StreamAudioCodec is not null
        && !string.Equals(d.SourceAudioCodec, d.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);

    public static bool HasResolutionDownscale(StreamDecisionDto? decision) =>
        decision?.SourceResolution is not null
        && decision.StreamResolution is not null
        && !string.Equals(decision.SourceResolution, decision.StreamResolution, StringComparison.OrdinalIgnoreCase);

    public static string OverallMode(StreamDecisionDto? decision, StreamDecisionHudLabels labels)
    {
        if (decision is null)
            return "";

        if (IsVideoTranscoded(decision) || IsAudioTranscoded(decision))
            return labels.Transcode;

        return decision.Mode switch
        {
            PlaybackMode.Direct => labels.Direct,
            PlaybackMode.Transmux => labels.Transmux,
            PlaybackMode.Transcode => labels.Transcode,
            _ => ""
        };
    }

    /// <summary>
    /// Keep the server decision in sync with the URL the player actually opened
    /// (Direct Play retry, remux ladder, quality step).
    /// </summary>
    public static StreamDecisionDto? Align(
        StreamDecisionDto? baseline,
        string? url,
        string? mimeType,
        bool isOriginalQuality)
    {
        if (LocalPlaybackUrl.IsLocalFile(url))
            return AlignMode(baseline, PlaybackMode.Direct, extraReason: TranscodeReason.None);

        var isHls = StreamingSourceKind.IsHls(mimeType, url);
        var isDirect = url is not null
            && url.Contains("/direct-stream", StringComparison.OrdinalIgnoreCase);

        if (isDirect)
            return AlignMode(baseline, PlaybackMode.Direct, extraReason: TranscodeReason.None);

        if (isHls && !isOriginalQuality)
            return AlignMode(baseline, PlaybackMode.Transcode, TranscodeReason.QualityDownscale);

        if (isHls)
        {
            if (baseline is not null && IsVideoTranscoded(baseline) && baseline.Mode == PlaybackMode.Transcode)
                return baseline;

            return AlignMode(baseline, PlaybackMode.Transmux, extraReason: TranscodeReason.None);
        }

        return baseline;
    }

    public static string FormatReason(TranscodeReason reason, StreamDecisionHudLabels labels)
    {
        if (reason == TranscodeReason.None)
            return "";

        var parts = new List<string>();
        if (reason.HasFlag(TranscodeReason.VideoCodecNotSupported))
            parts.Add(labels.ReasonVideoCodec);
        if (reason.HasFlag(TranscodeReason.AudioCodecNotSupported))
            parts.Add(labels.ReasonAudioCodec);
        if (reason.HasFlag(TranscodeReason.ContainerNotSupported))
            parts.Add(labels.ReasonContainer);
        if (reason.HasFlag(TranscodeReason.HlsSegmentsUnavailable))
            parts.Add(labels.ReasonHlsSegments);
        if (reason.HasFlag(TranscodeReason.SubtitlesBurnIn))
            parts.Add(labels.ReasonSubtitles);
        if (reason.HasFlag(TranscodeReason.ResolutionNotSupported))
            parts.Add(labels.ReasonResolution);
        if (reason.HasFlag(TranscodeReason.QualityDownscale))
            parts.Add(labels.ReasonQualityDownscale);
        return string.Join(", ", parts);
    }

    public static string FormatResolution(StreamDecisionDto decision)
    {
        var source = decision.SourceResolution;
        var stream = decision.StreamResolution;
        if (string.IsNullOrEmpty(source))
            return stream ?? "";
        if (string.IsNullOrEmpty(stream)
            || string.Equals(source, stream, StringComparison.OrdinalIgnoreCase))
            return source;
        return source + " -> " + stream;
    }

    public static StreamDecisionHudLabels CurrentLabels()
    {
        var french = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("fr", StringComparison.OrdinalIgnoreCase);
        return french ? StreamDecisionHudLabels.French : StreamDecisionHudLabels.English;
    }

    private static StreamDecisionDto? AlignMode(
        StreamDecisionDto? baseline,
        PlaybackMode mode,
        TranscodeReason extraReason)
    {
        if (baseline is null)
        {
            return extraReason == TranscodeReason.None && mode == PlaybackMode.Direct
                ? new StreamDecisionDto { Mode = mode }
                : new StreamDecisionDto { Mode = mode, Reason = extraReason };
        }

        var reason = baseline.Reason;
        if (extraReason != TranscodeReason.None)
            reason |= extraReason;
        else if (mode is PlaybackMode.Direct or PlaybackMode.Transmux)
            reason &= ~TranscodeReason.QualityDownscale;

        if (baseline.Mode == mode && baseline.Reason == reason)
            return baseline;

        return baseline with { Mode = mode, Reason = reason };
    }
}

public readonly record struct StreamDecisionHudLabels(
    string Direct,
    string Transmux,
    string Transcode,
    string Sidecar,
    string BurnIn,
    string Hardware,
    string Software,
    string ReasonVideoCodec,
    string ReasonAudioCodec,
    string ReasonContainer,
    string ReasonHlsSegments,
    string ReasonSubtitles,
    string ReasonResolution,
    string ReasonQualityDownscale)
{
    public static StreamDecisionHudLabels English { get; } = new(
        Direct: "Direct",
        Transmux: "Transmux",
        Transcode: "Transcode",
        Sidecar: "Sidecar",
        BurnIn: "Burn-in",
        Hardware: "hardware",
        Software: "software",
        ReasonVideoCodec: "Video codec not supported",
        ReasonAudioCodec: "Audio codec not supported",
        ReasonContainer: "Container not supported",
        ReasonHlsSegments: "HLS segments unavailable",
        ReasonSubtitles: "Subtitle burn-in",
        ReasonResolution: "Resolution not supported",
        ReasonQualityDownscale: "Reduced quality");

    public static StreamDecisionHudLabels French { get; } = new(
        Direct: "Direct",
        Transmux: "Transmux",
        Transcode: "Transcode",
        Sidecar: "Piste",
        BurnIn: "Incrustes",
        Hardware: "materiel",
        Software: "logiciel",
        ReasonVideoCodec: "Codec video non supporte",
        ReasonAudioCodec: "Codec audio non supporte",
        ReasonContainer: "Conteneur non supporte",
        ReasonHlsSegments: "Segments HLS non disponibles",
        ReasonSubtitles: "Incrustation des sous-titres",
        ReasonResolution: "Resolution non supportee",
        ReasonQualityDownscale: "Qualite reduite");
}
