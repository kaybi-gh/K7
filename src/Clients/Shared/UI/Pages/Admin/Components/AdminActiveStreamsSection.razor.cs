using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Pages.Admin.Panels;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class AdminActiveStreamsSection : IDisposable
{
    private IReadOnlyList<ActiveStreamDto>? _streams;
    private ActiveStreamDto? _selectedStream;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        HubClient.ActiveStreamsUpdated += OnActiveStreamsUpdated;

        await FetchStreamsAsync();

        try
        {
            await HubClient.JoinAdminStreamsGroupAsync();
        }
        catch
        {
        }
    }

    private void OnActiveStreamsUpdated(IReadOnlyList<ActiveStreamDto> streams)
    {
        InvokeAsync(() =>
        {
            _streams = streams;
            _loading = false;

            if (_selectedStream is not null)
            {
                _selectedStream = streams.FirstOrDefault(s => s.ConnectionId == _selectedStream.ConnectionId);
            }

            StateHasChanged();
        });
    }

    private async Task FetchStreamsAsync()
    {
        _loading = _streams is null;

        try
        {
            _streams = await K7ServerService.GetActiveStreamsAsync();
        }
        catch
        {
            _streams = null;
        }

        _loading = false;
    }

    private void OnStreamSelected(ActiveStreamDto stream)
    {
        _selectedStream = _selectedStream?.ConnectionId == stream.ConnectionId ? null : stream;
    }

    private void CloseDetail()
    {
        _selectedStream = null;
    }

    private StreamDetailModel BuildDetailModel(ActiveStreamDto stream)
    {
        var sd = stream.StreamDecision;
        var hasStream = sd is not null;

        var videoIsTranscoded = sd?.SourceVideoCodec is not null
            && sd.StreamVideoCodec is not null
            && !string.Equals(sd.SourceVideoCodec, sd.StreamVideoCodec, StringComparison.OrdinalIgnoreCase);
        var audioIsTranscoded = sd?.SourceAudioCodec is not null
            && sd!.StreamAudioCodec is not null
            && !string.Equals(sd.SourceAudioCodec, sd.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);

        string? modeLabel = null;
        string? modeBadgeVariant = null;
        if (hasStream)
        {
            if (videoIsTranscoded || audioIsTranscoded)
            {
                modeLabel = "Transcode";
                modeBadgeVariant = "transcode";
            }
            else if (sd!.Mode == PlaybackMode.Direct)
            {
                modeLabel = "Direct";
                modeBadgeVariant = "direct";
            }
            else
            {
                modeLabel = "Transmux";
                modeBadgeVariant = "transmux";
            }
        }

        string? stateLabel = stream.State switch
        {
            (int)PlaybackState.Playing => L["Playing"].Value,
            (int)PlaybackState.Paused => L["Paused"].Value,
            (int)PlaybackState.Buffering => L["Buffering"].Value,
            _ => null
        };
        string? stateVariant = stream.State switch
        {
            (int)PlaybackState.Playing => "success",
            (int)PlaybackState.Paused => "warning",
            (int)PlaybackState.Buffering => "warning",
            _ => null
        };

        return new StreamDetailModel
        {
            MediaTitle = stream.MediaTitle,
            MediaType = stream.MediaType,
            Status = stateLabel,
            StatusVariant = stateVariant,
            StartedAt = stream.StartedAt,
            DurationDisplay = $"{FormatTime(stream.Position)} / {FormatTime(stream.Duration)}",
            UserName = stream.UserName,
            DeviceName = stream.DeviceName,
            DeviceClient = stream.DeviceType,
            HasStreamDetails = hasStream,
            ModeLabel = modeLabel,
            ModeBadgeVariant = modeBadgeVariant,
            VideoDecision = videoIsTranscoded ? "Transcode" : "Direct",
            AudioDecision = audioIsTranscoded ? "Transcode" : "Direct",
            SourceVideoCodec = sd?.SourceVideoCodec,
            SourceAudioCodec = sd?.SourceAudioCodec,
            StreamVideoCodec = sd?.StreamVideoCodec,
            StreamAudioCodec = sd?.StreamAudioCodec,
            Resolution = sd?.SourceResolution,
            TranscodeReason = sd?.Reason is not null and not TranscodeReason.None ? FormatReason(sd.Reason) : null,
            Bitrate = sd?.Bitrate is > 0 ? FormatBitrate(sd.Bitrate.Value) : null,
            AudioTrackLanguage = sd?.AudioTrackLanguage,
            AudioTrackTitle = sd?.AudioTrackTitle,
            AudioChannelLayout = sd?.AudioChannelLayout,
            SubtitleTrackLanguage = sd?.SubtitleTrackLanguage,
            SubtitleTrackTitle = sd?.SubtitleTrackTitle
        };
    }

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
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

    public void Dispose()
    {
        HubClient.ActiveStreamsUpdated -= OnActiveStreamsUpdated;

        _ = HubClient.LeaveAdminStreamsGroupAsync();
    }
}
