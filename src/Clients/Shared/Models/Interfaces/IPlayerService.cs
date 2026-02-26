using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Domain.Interfaces;

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

    IReadOnlyList<AudioFileTrackDto> AudioTracks { get; }
    AudioFileTrackDto? SelectedAudioTrack { get; }

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


    Task ShowAsync();
    Task HideAsync();

    Task PlayIndexedFileAsync(Guid indexedFileId, IEnumerable<AudioFileTrackDto> audioTracks, int? audioTrackIndex = null, CancellationToken cancellationToken = default);
    Task ChangeAudioTrackAsync(AudioFileTrackDto track, CancellationToken cancellationToken = default);

}
