using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class AdminStreamCard
{
    [Parameter, EditorRequired]
    public ActiveStreamDto Stream { get; set; } = default!;

    private bool IsMusic => Stream.MediaType is "MusicTrack" or "MusicAlbum";

    private string CardVariantClass => IsMusic ? "stream-card--music" : "stream-card--video";

    private string PlaceholderIcon => IsMusic ? Phosphor.MusicNote : Phosphor.FilmSlate;

    private string ModeLabel => Stream.StreamDecision?.Mode switch
    {
        PlaybackMode.Direct => "Direct",
        PlaybackMode.Transmux => "Transmux",
        PlaybackMode.Transcode => "Transcode",
        _ => ""
    };

    private string ModeBadgeClass => Stream.StreamDecision?.Mode switch
    {
        PlaybackMode.Direct => "stream-card__mode-badge--direct",
        PlaybackMode.Transmux => "stream-card__mode-badge--transmux",
        PlaybackMode.Transcode => "stream-card__mode-badge--transcode",
        _ => ""
    };

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

            // Avoid redundancy when device name already contains the type
            if (name.Contains(Stream.DeviceType, StringComparison.OrdinalIgnoreCase))
                return name;

            return $"{name} ({Stream.DeviceType})";
        }
    }

    private string? MediaRoute
    {
        get
        {
            if (!Stream.MediaId.HasValue)
                return null;

            var id = Stream.MediaId.Value;

            return Stream.MediaType switch
            {
                "Movie" => $"/movies/{id}",
                "Serie" => $"/series/{id}",
                "SerieEpisode" when Stream.ParentId.HasValue => $"/series/{Stream.ParentId.Value}",
                "MusicAlbum" => $"/music/albums/{id}",
                "MusicTrack" when Stream.ParentId.HasValue => $"/music/albums/{Stream.ParentId.Value}",
                _ => null
            };
        }
    }

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
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
}
