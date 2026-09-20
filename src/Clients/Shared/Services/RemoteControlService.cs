using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Clients.Shared.Services;

public sealed class RemoteControlService : IRemoteControlService, IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly ICastService _castService;
    private DateTime _lastLocalVolumeUtc;
    private DateTime _sessionStartedUtc;
    private int _consecutiveStoppedReports;
    private bool _holdPositionOnPause;
    private DateTime _lastLocalSeekUtc;
    private double? _pendingSeekPosition;

    public RemoteControlService(K7HubClient hubClient, ICastService castService)
    {
        _hubClient = hubClient;
        _castService = castService;
        _hubClient.RemotePlaybackStateReceived += OnRemotePlaybackStateReceived;
        _castService.MediaStatusUpdated += OnCastMediaStatusUpdated;
    }

    public bool IsControlling { get; private set; }
    public bool IsAudio { get; private set; }
    public bool IsCastSession { get; private set; }
    public Guid? TargetDeviceId { get; private set; }
    public string? TargetDeviceName { get; private set; }

    public RemotePlaybackState PlaybackState { get; private set; }
    public double Position { get; private set; }
    public double Duration { get; private set; }
    public double Volume { get; private set; }
    public double PlaybackRate { get; private set; } = 1;
    public AspectRatioMode AspectRatio { get; private set; } = AspectRatioMode.Fit;
    public int? SelectedAudioTrackIndex { get; private set; }
    public int? SelectedSubtitleTrackIndex { get; private set; }
    public IReadOnlyList<RemoteTrackInfoDto> AudioTracks { get; private set; } = [];
    public IReadOnlyList<RemoteTrackInfoDto> SubtitleTracks { get; private set; } = [];
    public IReadOnlyList<ChapterMarkerDto> Chapters { get; private set; } = [];

    public string? Title { get; private set; }
    public string? Artist { get; private set; }
    public string? AlbumTitle { get; private set; }
    public string? CoverUrl { get; private set; }
    public string? ThumbnailsUrl { get; private set; }
    public Guid? MediaId { get; private set; }
    public Guid? IndexedFileId { get; private set; }

    public event Action? SessionChanged;
    public event Action? StateChanged;

    public void StartSession(Guid targetDeviceId, string targetDeviceName, RemotePlaybackRequestDto request)
    {
        TargetDeviceId = targetDeviceId;
        TargetDeviceName = targetDeviceName;
        IsAudio = request.IsAudio;
        IsCastSession = false;
        IsControlling = true;
        _sessionStartedUtc = DateTime.UtcNow;
        _consecutiveStoppedReports = 0;

        Title = request.Title;
        Artist = request.Artist;
        AlbumTitle = request.AlbumTitle;
        CoverUrl = request.CoverUrl;
        ThumbnailsUrl = request.ThumbnailsUrl;
        MediaId = request.MediaId;
        IndexedFileId = request.IndexedFileId;
        Duration = request.Duration ?? 0;
        Position = request.StartPosition ?? 0;
        PlaybackState = RemotePlaybackState.Playing;
        Volume = request.Volume is double v ? Math.Clamp(v, 0, 1) : 1.0;
        PlaybackRate = 1;
        AspectRatio = AspectRatioMode.Fit;
        _holdPositionOnPause = false;

        AudioTracks = [];
        SubtitleTracks = [];
        Chapters = [];
        SelectedAudioTrackIndex = null;
        SelectedSubtitleTrackIndex = null;

        SessionChanged?.Invoke();
    }

    public void StartCastSession(string deviceName, bool isAudio, string? title, string? artist, string? albumTitle, string? coverUrl, double duration, double startPosition)
    {
        TargetDeviceId = null;
        TargetDeviceName = deviceName;
        IsAudio = isAudio;
        IsCastSession = true;
        IsControlling = true;
        _sessionStartedUtc = DateTime.UtcNow;
        _consecutiveStoppedReports = 0;

        Title = title;
        Artist = artist;
        AlbumTitle = albumTitle;
        CoverUrl = coverUrl;
        ThumbnailsUrl = null;
        MediaId = null;
        IndexedFileId = null;
        Duration = duration;
        Position = startPosition;
        PlaybackState = RemotePlaybackState.Playing;
        Volume = 1.0;
        PlaybackRate = 1;
        AspectRatio = AspectRatioMode.Fit;

        AudioTracks = [];
        SubtitleTracks = [];
        Chapters = [];
        SelectedAudioTrackIndex = null;
        SelectedSubtitleTrackIndex = null;

        SessionChanged?.Invoke();
    }

    public void EndSession()
    {
        IsControlling = false;
        IsCastSession = false;
        TargetDeviceId = null;
        TargetDeviceName = null;
        _pendingSeekPosition = null;
        SessionChanged?.Invoke();
    }

    public async Task SendPlayAsync()
    {
        _holdPositionOnPause = false;
        if (IsCastSession)
        {
            await _castService.SendTransportCommandAsync(CastTransportCommand.Play);
            return;
        }
        if (TargetDeviceId is null) return;
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.Play
        });
    }

    public async Task SendPauseAsync()
    {
        _holdPositionOnPause = true;
        PlaybackState = RemotePlaybackState.Paused;
        StateChanged?.Invoke();

        if (IsCastSession)
        {
            await _castService.SendTransportCommandAsync(CastTransportCommand.Pause);
            return;
        }
        if (TargetDeviceId is null) return;
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.Pause
        });
    }

    public async Task SendStopAsync()
    {
        if (IsCastSession)
        {
            await _castService.SendTransportCommandAsync(CastTransportCommand.Stop);
            EndSession();
            return;
        }
        if (TargetDeviceId is null) return;
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.Stop
        });
        EndSession();
    }

    public async Task ReleaseControlAsync()
    {
        if (IsCastSession)
        {
            EndSession();
            return;
        }

        if (TargetDeviceId is Guid targetId)
        {
            await _hubClient.SendRemoteTransportCommandAsync(targetId, new RemoteTransportCommandDto
            {
                Action = RemoteTransportAction.ReleaseControl
            });
        }

        EndSession();
    }

    public Task SendSeekAsync(double position)
    {
        _holdPositionOnPause = false;
        if (Duration > 0)
            position = Math.Clamp(position, 0, Duration);
        else
            position = Math.Max(0, position);

        Position = position;
        _lastLocalSeekUtc = DateTime.UtcNow;
        _pendingSeekPosition = position;
        StateChanged?.Invoke();

        if (IsCastSession)
            return Task.CompletedTask;

        if (TargetDeviceId is null)
            return Task.CompletedTask;

        // Do not await the hub: SeekBar commit runs on the Blazor sync context and an
        // awaited round-trip blocked Enter on pause/stop/menu until the receiver answered.
        var deviceId = TargetDeviceId.Value;
        _ = SendSeekCommandAsync(deviceId, position);
        return Task.CompletedTask;
    }

    private async Task SendSeekCommandAsync(Guid deviceId, double position)
    {
        try
        {
            await _hubClient.SendRemoteTransportCommandAsync(deviceId, new RemoteTransportCommandDto
            {
                Action = RemoteTransportAction.SeekTo,
                Value = position
            });
        }
        catch
        {
            // Receiver may be gone; local optimistic Position already updated.
        }
    }

    public async Task SendVolumeAsync(double volume)
    {
        Volume = Math.Clamp(volume, 0, 1);
        _lastLocalVolumeUtc = DateTime.UtcNow;
        StateChanged?.Invoke();

        if (IsCastSession)
        {
            // Cast volume handled by JS
            return;
        }
        if (TargetDeviceId is null) return;
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SetVolume,
            Value = Volume
        });
    }

    public async Task SendAudioTrackAsync(int trackIndex)
    {
        if (IsCastSession || TargetDeviceId is null) return;
        SelectedAudioTrackIndex = trackIndex;
        StateChanged?.Invoke();
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SetAudioTrack,
            TrackIndex = trackIndex
        });
    }

    public async Task SendSubtitleTrackAsync(int trackIndex)
    {
        if (IsCastSession || TargetDeviceId is null) return;
        SelectedSubtitleTrackIndex = trackIndex;
        StateChanged?.Invoke();
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SetSubtitleTrack,
            TrackIndex = trackIndex
        });
    }

    public async Task SendPlaybackRateAsync(double rate)
    {
        if (IsCastSession || TargetDeviceId is null) return;
        PlaybackRate = rate;
        StateChanged?.Invoke();
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SetPlaybackRate,
            Value = rate
        });
    }

    public async Task SendAspectRatioAsync(AspectRatioMode mode)
    {
        if (IsCastSession || TargetDeviceId is null) return;
        AspectRatio = mode;
        StateChanged?.Invoke();
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SetAspectRatio,
            Value = (int)mode
        });
    }

    private void OnRemotePlaybackStateReceived(RemotePlaybackStateDto state)
    {
        if (IsCastSession) return;

        PlaybackState = state.State;

        // Keep duration sticky: receivers often report 0 while seeking / buffering.
        if (state.Duration > 0)
            Duration = state.Duration;

        var seekAge = (DateTime.UtcNow - _lastLocalSeekUtc).TotalSeconds;
        if (_pendingSeekPosition is double pending
            && seekAge < 3
            && Math.Abs(state.Position - pending) > 2)
        {
            // Keep optimistic thumb at the scrub target until the receiver catches up.
            Position = pending;
        }
        else
        {
            _pendingSeekPosition = null;

            if (!(_holdPositionOnPause
                && state.State == RemotePlaybackState.Paused
                && state.Position + 2 < Position))
            {
                Position = state.Position;
                if (state.State != RemotePlaybackState.Paused)
                    _holdPositionOnPause = false;
            }
            else if (state.State != RemotePlaybackState.Paused)
            {
                Position = state.Position;
                _holdPositionOnPause = false;
            }
        }

        if (Duration > 0 && Position > Duration)
            Position = Duration;

        // Keep the slider stable for a moment after the user moves it; otherwise 1 Hz
        // reports from the receiver yank the thumb / focus back.
        if ((DateTime.UtcNow - _lastLocalVolumeUtc).TotalSeconds > 1.5)
            Volume = state.Volume;
        SelectedAudioTrackIndex = state.SelectedAudioTrackIndex;
        SelectedSubtitleTrackIndex = state.SelectedSubtitleTrackIndex;

        if (state.AudioTracks is not null && !TrackListsEqual(AudioTracks, state.AudioTracks))
            AudioTracks = state.AudioTracks;
        if (state.SubtitleTracks is not null && !TrackListsEqual(SubtitleTracks, state.SubtitleTracks))
            SubtitleTracks = state.SubtitleTracks;

        var seekMetadataChanged = false;
        var thumbnailsChanged = !string.Equals(ThumbnailsUrl, state.ThumbnailsUrl, StringComparison.Ordinal);
        if (thumbnailsChanged && !string.IsNullOrEmpty(state.ThumbnailsUrl))
        {
            ThumbnailsUrl = state.ThumbnailsUrl;
            seekMetadataChanged = true;
        }

        if (state.Chapters is not null && !ChaptersEqual(Chapters, state.Chapters))
        {
            Chapters = state.Chapters;
            seekMetadataChanged = true;
        }

        if (state.PlaybackRate > 0)
            PlaybackRate = state.PlaybackRate;
        if (Enum.IsDefined(typeof(AspectRatioMode), state.AspectRatio))
            AspectRatio = (AspectRatioMode)state.AspectRatio;

        if (state.State == RemotePlaybackState.Stopped)
        {
            // Audio/quality reloads briefly report Idle->Stopped. Ignore early flaps or the
            // remote UI never appears (attach) / vanishes mid-session (track change).
            _consecutiveStoppedReports++;
            var attachGrace = (DateTime.UtcNow - _sessionStartedUtc).TotalSeconds < 4;
            if (attachGrace || _consecutiveStoppedReports < 2)
            {
                StateChanged?.Invoke();
                return;
            }

            EndSession();
            return;
        }

        _consecutiveStoppedReports = 0;

        if (seekMetadataChanged)
            SessionChanged?.Invoke();
        StateChanged?.Invoke();
    }

    private static bool TrackListsEqual(
        IReadOnlyList<RemoteTrackInfoDto> current,
        IReadOnlyList<RemoteTrackInfoDto> incoming)
    {
        if (ReferenceEquals(current, incoming))
            return true;
        if (current.Count != incoming.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].Index != incoming[i].Index)
                return false;
            if (!string.Equals(current[i].Label, incoming[i].Label, StringComparison.Ordinal))
                return false;
            if (!string.Equals(current[i].Language, incoming[i].Language, StringComparison.Ordinal))
                return false;
            if (!string.Equals(current[i].Name, incoming[i].Name, StringComparison.Ordinal))
                return false;
            if (!string.Equals(current[i].Codec, incoming[i].Codec, StringComparison.Ordinal))
                return false;
            if (!string.Equals(current[i].ChannelLayout, incoming[i].ChannelLayout, StringComparison.Ordinal))
                return false;
            if (current[i].IsForced != incoming[i].IsForced)
                return false;
            if (current[i].IsHearingImpaired != incoming[i].IsHearingImpaired)
                return false;
        }

        return true;
    }

    private static bool ChaptersEqual(
        IReadOnlyList<ChapterMarkerDto> current,
        IReadOnlyList<ChapterMarkerDto> incoming)
    {
        if (ReferenceEquals(current, incoming))
            return true;
        if (current.Count != incoming.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (Math.Abs(current[i].StartSeconds - incoming[i].StartSeconds) > 0.05)
                return false;
            if (!string.Equals(current[i].Title, incoming[i].Title, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void OnCastMediaStatusUpdated(CastMediaStatus status)
    {
        if (!IsCastSession) return;

        PlaybackState = status.State switch
        {
            "playing" => RemotePlaybackState.Playing,
            "paused" => RemotePlaybackState.Paused,
            "buffering" => RemotePlaybackState.Buffering,
            _ => RemotePlaybackState.Stopped
        };
        Position = status.Position;
        if (status.Duration > 0)
            Duration = status.Duration;
        Volume = status.Volume;

        if (PlaybackState == RemotePlaybackState.Stopped)
        {
            EndSession();
            return;
        }

        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        _hubClient.RemotePlaybackStateReceived -= OnRemotePlaybackStateReceived;
        _castService.MediaStatusUpdated -= OnCastMediaStatusUpdated;
    }
}
