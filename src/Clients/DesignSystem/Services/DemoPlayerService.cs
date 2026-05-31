using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.DesignSystem.Services;

public sealed class DemoPlayerService : IPlayerService
{
    // Control events - wired to VideoPlayer component
    public event Func<Task>? PlayRequested;
    public event Func<Task>? PauseRequested;
    public event Func<Task>? StopRequested;
    public event Func<double, Task>? SeekRequested;
    public event Func<Task>? EnterFullScreenRequested;
    public event Func<Task>? ExitFullScreenRequested;
    public event Func<Task>? MuteRequested;
    public event Func<Task>? UnmuteRequest;
    public event Func<double, Task>? VolumeChangeRequested;
    public event Func<double, Task>? PlaybackRateChangeRequested;
    public event Action<AspectRatioMode>? AspectRatioModeChangeRequested;
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;

    // Unused in demo context
#pragma warning disable CS0067
    public event Action<string>? SwitchAudioTrackRequested;
    public event Action<string?>? SwitchSubtitleTrackRequested;

    // State feedback events - fired back by VideoPlayer via [JSInvokable]
    public event Action<bool>? IsFullScreenChanged;
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<double>? PlaybackRateChanged;
    public event Action<bool>? IsMutedChanged;
    public event Action<AudioFileTrackDto?>? AudioTrackChanged;
    public event Action<SubtitleFileTrackDto?>? SubtitleTrackChanged;
    public event Action<VideoQualityOption?>? QualityChanged;
    public event Action<AspectRatioMode>? AspectRatioModeChanged;
    public event Action? BackPressed;
#pragma warning restore CS0067

    public IReadOnlyList<AudioFileTrackDto> AudioTracks => [];
    public AudioFileTrackDto? SelectedAudioTrack => null;
    public IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks => [];
    public SubtitleFileTrackDto? SelectedSubtitleTrack => null;
    public IReadOnlyList<VideoQualityOption> AvailableQualities => [];
    public VideoQualityOption? SelectedQuality => null;

    public bool IsVisible { get; private set; }
    public PlayerSource Source { get; set; } = new();
    public PlaybackState PlaybackState { get; set; } = PlaybackState.Idle;
    public bool IsFullScreen { get; set; }
    public double Duration { get; set; }
    public double CurrentTime { get; set; }
    public double BufferedTime { get; set; }
    public double Volume { get; set; } = 1.0;
    public double PlaybackRate { get; set; } = 1.0;
    public bool IsMuted { get; set; }
    public AspectRatioMode AspectRatio => AspectRatioMode.Fit;

    public void Play() => _ = PlayRequested?.Invoke();
    public void Pause() => _ = PauseRequested?.Invoke();
    public void Stop() => _ = StopRequested?.Invoke();
    public void Seek(double time) => _ = SeekRequested?.Invoke(time);
    public void Mute() => _ = MuteRequested?.Invoke();
    public void Unmute() => _ = UnmuteRequest?.Invoke();
    public void SetVolume(double volume) => _ = VolumeChangeRequested?.Invoke(volume);
    public void SetPlaybackRate(double rate) => _ = PlaybackRateChangeRequested?.Invoke(rate);
    public void EnterFullScreen() => _ = EnterFullScreenRequested?.Invoke();
    public void ExitFullScreen() => _ = ExitFullScreenRequested?.Invoke();
    public void SetAspectRatioMode(AspectRatioMode mode) => AspectRatioModeChangeRequested?.Invoke(mode);

    public Task ShowAsync()
    {
        IsVisible = true;
        IsVisibleChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        IsVisible = false;
        IsVisibleChanged?.Invoke();
        return Task.CompletedTask;
    }

    // Sync audio player state into SeekBar (which reads IPlayerService)
    public void FireAudioState(double duration, double currentTime, K7.Server.Domain.Enums.PlaybackState state)
    {
        Duration = duration;
        CurrentTime = currentTime;
        PlaybackState = state;
        DurationChanged?.Invoke(duration);
        CurrentTimeChanged?.Invoke(currentTime);
        PlaybackStateChanged?.Invoke(state);
    }

    public async Task PlayFromUrlAsync(string url, string mimeType)
    {
        Source = new PlayerSource { Url = url, MimeType = mimeType };
        SourceChanged?.Invoke(Source);

        IsVisible = true;
        IsVisibleChanged?.Invoke();

        // VideoPlayer.PlayAsync() will set _playPending=true since it isn't initialized yet;
        // the flag is then picked up in OnAfterRenderAsync to autoplay.
        if (PlayRequested is not null)
            await PlayRequested.Invoke();
    }

    public Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PlayRemoteIndexedFileAsync(Guid remoteFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, Guid? mediaId = null, string? title = null, string? coverUrl = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void SetSubtitleTracks(IEnumerable<SubtitleFileTrackDto> tracks) { }

    public Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ChangeSubtitleTrackAsync(SubtitleFileTrackDto? track, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ChangeQualityAsync(VideoQualityOption? quality, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
