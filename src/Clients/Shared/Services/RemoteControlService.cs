using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Services;

public sealed class RemoteControlService : IRemoteControlService, IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly ICastService _castService;

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
    public int? SelectedAudioTrackIndex { get; private set; }
    public int? SelectedSubtitleTrackIndex { get; private set; }
    public IReadOnlyList<RemoteTrackInfoDto> AudioTracks { get; private set; } = [];
    public IReadOnlyList<RemoteTrackInfoDto> SubtitleTracks { get; private set; } = [];

    public string? Title { get; private set; }
    public string? Artist { get; private set; }
    public string? AlbumTitle { get; private set; }
    public string? CoverUrl { get; private set; }
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

        Title = request.Title;
        Artist = request.Artist;
        AlbumTitle = request.AlbumTitle;
        CoverUrl = request.CoverUrl;
        MediaId = request.MediaId;
        IndexedFileId = request.IndexedFileId;
        Duration = request.Duration ?? 0;
        Position = request.StartPosition ?? 0;
        PlaybackState = RemotePlaybackState.Playing;
        Volume = 1.0;

        AudioTracks = [];
        SubtitleTracks = [];
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

        Title = title;
        Artist = artist;
        AlbumTitle = albumTitle;
        CoverUrl = coverUrl;
        MediaId = null;
        IndexedFileId = null;
        Duration = duration;
        Position = startPosition;
        PlaybackState = RemotePlaybackState.Playing;
        Volume = 1.0;

        AudioTracks = [];
        SubtitleTracks = [];
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
        SessionChanged?.Invoke();
    }

    public async Task SendPlayAsync()
    {
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

    public async Task SendSeekAsync(double position)
    {
        Position = position;
        StateChanged?.Invoke();

        if (IsCastSession)
        {
            // Cast seek is done via JS interop directly - handled by CastOrchestrationService
            return;
        }
        if (TargetDeviceId is null) return;
        await _hubClient.SendRemoteTransportCommandAsync(TargetDeviceId.Value, new RemoteTransportCommandDto
        {
            Action = RemoteTransportAction.SeekTo,
            Value = position
        });
    }

    public async Task SendVolumeAsync(double volume)
    {
        Volume = volume;
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
            Value = volume
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

    private void OnRemotePlaybackStateReceived(RemotePlaybackStateDto state)
    {
        if (IsCastSession) return;

        PlaybackState = state.State;
        Position = state.Position;
        Duration = state.Duration;
        Volume = state.Volume;
        SelectedAudioTrackIndex = state.SelectedAudioTrackIndex;
        SelectedSubtitleTrackIndex = state.SelectedSubtitleTrackIndex;

        if (state.AudioTracks is not null)
            AudioTracks = state.AudioTracks;
        if (state.SubtitleTracks is not null)
            SubtitleTracks = state.SubtitleTracks;

        if (state.State == RemotePlaybackState.Stopped)
        {
            EndSession();
            return;
        }

        StateChanged?.Invoke();
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
