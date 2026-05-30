using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class AdminStreamCard
{
    [Parameter, EditorRequired]
    public ActiveStreamDto Stream { get; set; } = default!;

    [Parameter]
    public EventCallback<ActiveStreamDto> OnClick { get; set; }

    private bool IsMusic => Stream.MediaType is "MusicTrack" or "MusicAlbum";

    private string CardVariantClass => IsMusic ? "stream-card--music" : "stream-card--video";

    private string PlaceholderIcon => IsMusic ? Phosphor.MusicNote : Phosphor.FilmSlate;

    private bool IsVideoTranscoded => Stream.StreamDecision is { } d
        && d.SourceVideoCodec is not null
        && d.StreamVideoCodec is not null
        && !string.Equals(d.SourceVideoCodec, d.StreamVideoCodec, StringComparison.OrdinalIgnoreCase);

    private bool IsAudioTranscoded => Stream.StreamDecision is { } d
        && d.SourceAudioCodec is not null
        && d.StreamAudioCodec is not null
        && !string.Equals(d.SourceAudioCodec, d.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);

    private string OverallModeLabel
    {
        get
        {
            if (IsVideoTranscoded || IsAudioTranscoded) return "Transcode";
            return Stream.StreamDecision?.Mode switch
            {
                PlaybackMode.Direct => "Direct",
                PlaybackMode.Transmux => "Transmux",
                _ => ""
            };
        }
    }

    private string OverallModeBadgeClass
    {
        get
        {
            if (IsVideoTranscoded || IsAudioTranscoded) return "stream-card__mode-badge--transcode";
            return Stream.StreamDecision?.Mode switch
            {
                PlaybackMode.Direct => "stream-card__mode-badge--direct",
                PlaybackMode.Transmux => "stream-card__mode-badge--transmux",
                _ => ""
            };
        }
    }

    private double ProgressPercent => Stream.Duration > 0
        ? Stream.Position / Stream.Duration * 100
        : 0;

    private string UserInitial => Stream.UserName?.Length > 0
        ? Stream.UserName[0].ToString().ToUpperInvariant()
        : "?";

    private string DeviceLabel
    {
        get
        {
            var name = Stream.DeviceName ?? "-";
            if (string.IsNullOrEmpty(Stream.DeviceType) || Stream.DeviceType == "Unknown")
                return name;

            if (name.Contains(Stream.DeviceType, StringComparison.OrdinalIgnoreCase))
                return name;

            return $"{name} ({Stream.DeviceType})";
        }
    }

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    private string FormatRemainingTime()
    {
        var remaining = Stream.Duration - Stream.Position;
        if (remaining <= 0) return FormatTime(Stream.Duration);
        return $"-{FormatTime(remaining)}";
    }

    private static string FormatResolution(string resolution)
    {
        var parts = resolution.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[1], out var height))
        {
            return $"{height}p";
        }
        return resolution;
    }

    private static string FormatBitrate(int bitrate)
    {
        return bitrate >= 1000
            ? $"{bitrate / 1000.0:0.#} Mbps"
            : $"{bitrate} Kbps";
    }

    private string FormatReason(TranscodeReason reason)
    {
        var parts = new List<string>();

        if (reason.HasFlag(TranscodeReason.VideoCodecNotSupported))
            parts.Add(L["ReasonVideoCodec"]);
        if (reason.HasFlag(TranscodeReason.AudioCodecNotSupported))
            parts.Add(L["ReasonAudioCodec"]);
        if (reason.HasFlag(TranscodeReason.ContainerNotSupported))
            parts.Add(L["ReasonContainer"]);
        if (reason.HasFlag(TranscodeReason.HlsSegmentsUnavailable))
            parts.Add(L["ReasonHlsSegments"]);
        if (reason.HasFlag(TranscodeReason.SubtitlesBurnIn))
            parts.Add(L["ReasonSubtitles"]);
        if (reason.HasFlag(TranscodeReason.ResolutionNotSupported))
            parts.Add(L["ReasonResolution"]);

        return string.Join(", ", parts);
    }

    private async Task OnCardClicked()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(Stream);
    }
}
