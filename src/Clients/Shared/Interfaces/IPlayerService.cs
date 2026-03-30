using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Interfaces;

public interface IPlayerService
{
    event Func<Task>? PlayRequested;
    event Func<Task>? PauseRequested;
    event Func<Task>? StopRequested;
    event Func<double, Task>? SeekRequested;
    event Func<Task>? EnterFullScreenRequested;
    event Func<Task>? ExitFullScreenRequested;
    event Func<Task>? MuteRequested;
    event Func<Task>? UnmuteRequest;
    event Func<double, Task>? VolumeChangeRequested;
    event Func<double, Task>? PlaybackRateChangeRequested;
    event Action<AspectRatioMode>? AspectRatioModeChangeRequested;


    event Action<string>? SwitchAudioTrackRequested;
    event Action<string?>? SwitchSubtitleTrackRequested;
    event Action<PlayerSource>? SourceChanged;
    event Action? IsVisibleChanged;
    event Action<bool>? IsFullScreenChanged;
    event Action<PlaybackState>? PlaybackStateChanged;
    event Action<double>? DurationChanged;
    event Action<double>? CurrentTimeChanged;
    event Action<double>? BufferedTimeChanged;
    event Action<double>? VolumeChanged;
    event Action<double>? PlaybackRateChanged;
    event Action<bool>? IsMutedChanged;
    event Action<AudioFileTrackDto?>? AudioTrackChanged;
    event Action<SubtitleFileTrackDto?>? SubtitleTrackChanged;
    event Action<VideoQualityOption?>? QualityChanged;
    event Action<AspectRatioMode>? AspectRatioModeChanged;

    IReadOnlyList<AudioFileTrackDto> AudioTracks { get; }
    AudioFileTrackDto? SelectedAudioTrack { get; }

    IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks { get; }
    SubtitleFileTrackDto? SelectedSubtitleTrack { get; }

    IReadOnlyList<VideoQualityOption> AvailableQualities { get; }
    VideoQualityOption? SelectedQuality { get; }

    PlayerSource Source { get; set; }
    bool IsVisible { get; }
    PlaybackState PlaybackState { get; set; }
    bool IsFullScreen { get; set; }
    double Duration { get; set; }
    double CurrentTime { get; set; }
    double BufferedTime { get; set; }
    double Volume { get; set; }
    double PlaybackRate { get; set; }
    bool IsMuted { get; set; }
    AspectRatioMode AspectRatio { get; }

    void Play();
    void Pause();
    void EnterFullScreen();
    void ExitFullScreen();
    void Seek(double time);
    void Mute();
    void Unmute();
    void SetVolume(double volume);
    void SetPlaybackRate(double rate);
    void Stop();
    void SetAspectRatioMode(AspectRatioMode mode);


    Task ShowAsync();
    Task HideAsync();

    Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, IEnumerable<SubtitleFileTrackDto>? subtitleTracks = null, int? audioTrackIndex = null, int? subtitleTrackIndex = null, VideoResolutionIdentifier? videoResolution = null, string? thumbnailsUrl = null, CancellationToken cancellationToken = default);
    Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default);
    Task ChangeSubtitleTrackAsync(SubtitleFileTrackDto? track, CancellationToken cancellationToken = default);
    Task ChangeQualityAsync(VideoQualityOption? quality, CancellationToken cancellationToken = default);

}
