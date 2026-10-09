using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using K7.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class AdminStreamCard
{
    [Parameter, EditorRequired]
    public ActiveStreamDto Stream { get; set; } = default!;

    [Parameter]
    public EventCallback<ActiveStreamDto> OnClick { get; set; }

    [Parameter]
    public bool FederationEnabled { get; set; }

    private bool IsMusic => Stream.MediaType is "MusicTrack" or "MusicAlbum";

    private string CardVariantClass => IsMusic ? "stream-card--music" : "stream-card--video";

    private string PlaceholderIcon => IsMusic ? Phosphor.MusicNote : Phosphor.FilmSlate;

    private MediaCardVariant ArtworkVariant => IsMusic ? MediaCardVariant.Cover : MediaCardVariant.Poster;

    private bool HasMediaHref => MediaHref is not null;

    private string? MediaHref
    {
        get
        {
            if (Stream.MediaId is not Guid mediaId || !Enum.TryParse<MediaType>(Stream.MediaType, out var type))
                return null;

            return type switch
            {
                MediaType.MusicTrack => Stream.ParentId is Guid albumId
                    ? MediaPageUrls.Build(type, mediaId, albumId: albumId)
                    : null,
                MediaType.SerieEpisode => EpisodeHref(mediaId),
                MediaType.SerieSeason => SeasonHref(mediaId),
                _ => MediaPageUrls.Build(type, mediaId)
            };
        }
    }

    private string DetailsAriaLabel =>
        string.Format(L["ViewDetails"].Value, Stream.MediaTitle ?? "-");

    private MediaCardViewModel ArtworkModel => new()
    {
        Id = Stream.MediaId?.ToString() ?? Stream.ConnectionId,
        Title = Stream.MediaTitle,
        PictureUrl = Stream.ThumbnailUrl,
        Kind = IsMusic ? MediaCardKind.Cover : MediaCardKind.Poster
    };

    private bool ShowCompute => FederationEnabled;

    private string ComputeLabel => ShowExecutionBadge ? ExecutionBadgeLabel : L["ComputeLocal"].Value;

    /// <summary>Origin side of a federation pull (device type set by CreateFederationStreamSession).</summary>
    private bool IsFederationOriginSide =>
        string.Equals(Stream.DeviceType, "Federation", StringComparison.Ordinal);

    /// <summary>This server runs remux/transcode ffmpeg for the session.</summary>
    private bool IsLocalCompute => Stream.FederatedPlaybackExecution switch
    {
        null => true,
        FederatedPlaybackExecution.Peer => !IsFederationOriginSide,
        FederatedPlaybackExecution.Origin => IsFederationOriginSide,
        _ => true
    };

    private bool ShowExecutionBadge => Stream.FederatedPlaybackExecution is not null;

    private string ExecutionBadgeLabel => Stream.FederatedPlaybackExecution switch
    {
        FederatedPlaybackExecution.Peer when IsFederationOriginSide =>
            string.Format(L["ExecutionOnPeer"].Value, Stream.DeviceName ?? L["Peer"].Value),
        FederatedPlaybackExecution.Peer => L["ExecutionHere"].Value,
        FederatedPlaybackExecution.Origin when IsFederationOriginSide => L["ExecutionHere"].Value,
        FederatedPlaybackExecution.Origin => L["ExecutionOnOrigin"].Value,
        _ => L["Federation"].Value
    };

    private bool IsSubtitleBurnIn => IsLocalCompute
        && (Stream.StreamDecision is { IsSubtitleBurnIn: true }
            || Stream.StreamDecision?.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn) == true);

    private bool HasSubtitleTrack => Stream.StreamDecision is { } d
        && (IsSubtitleBurnIn
            || d.SubtitleTrackLanguage is not null
            || d.SubtitleTrackTitle is not null
            || d.SubtitleCodec is not null);

    private bool IsVideoTranscoded => IsLocalCompute
        && Stream.StreamDecision is { } d
        && (d.Mode == PlaybackMode.Transcode
            || IsSubtitleBurnIn
            || d.Reason.HasFlag(TranscodeReason.ResolutionNotSupported)
            || d.Reason.HasFlag(TranscodeReason.QualityDownscale)
            || HasResolutionDownscale(d)
            || (d.SourceVideoCodec is not null
                && d.StreamVideoCodec is not null
                && !string.Equals(d.SourceVideoCodec, d.StreamVideoCodec, StringComparison.OrdinalIgnoreCase)));

    private static bool HasResolutionDownscale(StreamDecisionDto decision) =>
        decision.SourceResolution is not null
        && decision.StreamResolution is not null
        && !string.Equals(decision.SourceResolution, decision.StreamResolution, StringComparison.OrdinalIgnoreCase);

    private bool HasVideoEncoderInfo => Stream.StreamDecision?.VideoEncoder is not null
        || Stream.StreamDecision?.IsHardwareAccelerated is not null;

    private bool HasAudioEncoderInfo => Stream.StreamDecision?.AudioEncoder is not null;

    private bool IsHardwareEncoder => Stream.StreamDecision?.IsHardwareAccelerated == true;

    private bool IsAudioTranscoded => IsLocalCompute
        && Stream.StreamDecision is { } d
        && d.SourceAudioCodec is not null
        && d.StreamAudioCodec is not null
        && !string.Equals(d.SourceAudioCodec, d.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);

    private string? PosterStatus
    {
        get
        {
            if (Stream.StreamDecision is null)
                return null;
            if (!IsLocalCompute)
                return "direct";
            if (IsVideoTranscoded || IsAudioTranscoded)
                return "transcode";
            return Stream.StreamDecision.Mode switch
            {
                PlaybackMode.Direct => "direct",
                PlaybackMode.Transmux => "transmux",
                _ => null
            };
        }
    }

    private string PosterStatusLabel => PosterStatus switch
    {
        "direct" => L["DirectPlay"].Value,
        "transcode" => L["Transcode"].Value,
        "transmux" => L["Transmux"].Value,
        _ => ""
    };

    private string VideoDecisionLabel =>
        DecisionLabel(IsVideoTranscoded, HasVideoEncoderInfo, IsHardwareEncoder);

    private string AudioDecisionLabel =>
        DecisionLabel(IsAudioTranscoded, HasAudioEncoderInfo, hardware: false);

    private bool ShowProgress =>
        !string.Equals(Stream.DeviceClient, "External", StringComparison.OrdinalIgnoreCase)
        || Stream.HasPlaybackProgress;

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
            var client = FormatDeviceClient(Stream.DeviceClient);
            var parts = new List<string> { name };

            if (!string.IsNullOrWhiteSpace(client)
                && !name.Contains(client, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(client);
            }
            else if (!string.IsNullOrEmpty(Stream.DeviceType)
                     && Stream.DeviceType != "Unknown"
                     && !name.Contains(Stream.DeviceType, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(Stream.DeviceType);
            }

            return string.Join(" · ", parts);
        }
    }

    private string? FormatDeviceClient(string? client) => client switch
    {
        "External" => L["ClientExternal"],
        "Native" => L["ClientNative"],
        "Web" => L["ClientWeb"],
        _ => client
    };

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

    private string DecisionLabel(bool transcoded, bool hasMethod, bool hardware)
    {
        if (!transcoded)
            return L["DirectPlay"].Value;
        if (!hasMethod)
            return L["Transcode"].Value;

        var method = hardware ? L["Hardware"] : L["Software"];
        return string.Format(L["TranscodeWithMethod"].Value, method.Value);
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
        if (reason.HasFlag(TranscodeReason.QualityDownscale))
            parts.Add(L["ReasonQualityDownscale"]);

        return string.Join(", ", parts);
    }

    private async Task OnCardClicked()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(Stream);
    }

    private string? EpisodeHref(Guid mediaId)
    {
        if (Stream.ParentId is not Guid serieId)
            return null;
        if (Stream.SeasonNumber is int season && Stream.EpisodeNumber is int episode)
            return MediaPageUrls.Build(MediaType.SerieEpisode, mediaId, serieId, season, episode);
        return MediaPageUrls.Build(MediaType.Serie, serieId);
    }

    private string? SeasonHref(Guid mediaId)
    {
        if (Stream.ParentId is Guid serieId && Stream.SeasonNumber is int season)
            return MediaPageUrls.Build(MediaType.SerieSeason, mediaId, serieId, season);
        return Stream.ParentId is Guid parentId
            ? MediaPageUrls.Build(MediaType.Serie, parentId)
            : MediaPageUrls.Build(MediaType.SerieSeason, mediaId);
    }

    private async Task OnCardKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != " ")
            return;

        await OnCardClicked();
    }
}
