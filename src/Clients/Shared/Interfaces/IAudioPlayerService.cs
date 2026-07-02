using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Interfaces;

public interface IAudioPlayerService
{
    // Transport events
    event Func<Task>? PlayRequested;
    event Func<Task>? PauseRequested;
    event Func<Task>? StopRequested;
    event Func<double, Task>? SeekRequested;
    event Func<Task>? MuteRequested;
    event Func<Task>? UnmuteRequested;
    event Func<double, Task>? VolumeChangeRequested;

    // State change events
    event Action<PlayerSource>? SourceChanged;
    event Action? IsVisibleChanged;
    event Action<PlaybackState>? PlaybackStateChanged;
    event Action<double>? DurationChanged;
    event Action<double>? CurrentTimeChanged;
    event Action<double>? BufferedTimeChanged;
    event Action<double>? VolumeChanged;
    event Action<bool>? IsMutedChanged;

    // Queue events
    event Action? QueueChanged;
    event Action<AudioQueueItem?>? CurrentTrackChanged;
    event Action<RepeatMode>? RepeatModeChanged;
    event Action<bool>? ShuffleChanged;

    // Playback state
    PlaybackState PlaybackState { get; set; }
    double Duration { get; set; }
    double CurrentTime { get; set; }
    double BufferedTime { get; set; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    bool IsVisible { get; }

    // Queue state
    IReadOnlyList<AudioQueueItem> Queue { get; }
    AudioQueueItem? CurrentTrack { get; }
    int CurrentIndex { get; }
    RepeatMode Repeat { get; }
    bool Shuffle { get; }
    string? ActiveRadioTitle { get; }
    Guid? ActivePlaylistId { get; }
    event Action? ActiveRadioChanged;
    event Action? ActivePlaylistChanged;

    // Transport controls
    void Play();
    void Pause();
    void Stop();
    void Seek(double time);
    void Mute();
    void Unmute();
    void SetVolume(double volume);

    // Queue management
    Task PlayTrackAsync(AudioQueueItem track, CancellationToken cancellationToken = default);
    Task PlayTracksAsync(IEnumerable<AudioQueueItem> tracks, int startIndex = 0, Guid? playlistId = null, CancellationToken cancellationToken = default);
    Task PlayRadioAsync(IEnumerable<AudioQueueItem> tracks, string radioTitle, int startIndex = 0, CancellationToken cancellationToken = default);
    void AddToQueue(AudioQueueItem track);
    void AddToQueueNext(AudioQueueItem track);
    void RemoveFromQueue(int index);
    void ClearQueue();

    // Navigation
    Task SkipToIndexAsync(int index, CancellationToken cancellationToken = default);
    Task NextAsync(CancellationToken cancellationToken = default);
    Task PreviousAsync(CancellationToken cancellationToken = default);

    // Modes
    void ToggleShuffle();
    void CycleRepeatMode();

    // Crossfade
    bool AdaptiveCrossfade { get; }
    double CrossfadeDuration { get; }
    double CrossfadeTriggerWindow { get; }
    event Func<PlayerSource, double, Task>? CrossfadeRequested;
    event Action? CrossfadeDurationChanged;
    event Func<PlayerSource, Task>? GaplessPrebufferRequested;
    void ToggleAdaptiveCrossfade();
    void SetCrossfadeDuration(double seconds);
    Task OnCrossfadeNeededAsync(CancellationToken cancellationToken = default);
    Task OnGaplessPrebufferNeededAsync(CancellationToken cancellationToken = default);

    // Loudness normalization
    bool LoudnessEnabled { get; }
    double LoudnessTargetLufs { get; }
    double LoudnessPreampDb { get; }
    bool LimiterEnabled { get; }
    void SetLoudnessEnabled(bool enabled);
    void SetLoudnessTargetLufs(double lufs);
    void SetLoudnessPreampDb(double db);
    void SetLimiterEnabled(bool enabled);
    event Action? LoudnessSettingsChanged;

    // Equalizer
    bool EqEnabled { get; }
    double[] EqBands { get; }
    string? EqPresetName { get; }
    void SetEqEnabled(bool enabled);
    void SetEqBands(double[] bands);
    void SetEqPresetName(string? name);
    event Action? EqSettingsChanged;

    // Full screen
    bool IsFullScreenVisible { get; }
    bool ShowFullscreenOnPlay { get; }
    event Action? IsFullScreenVisibleChanged;
    void ToggleFullScreen();
    void SetShowFullscreenOnPlay(bool enabled);

    // Player UX preferences
    int SkipBackSeconds { get; }
    int SkipForwardSeconds { get; }
    bool KeepScreenOn { get; }
    void SetSkipBackSeconds(int seconds);
    void SetSkipForwardSeconds(int seconds);
    void SetKeepScreenOn(bool enabled);
    event Action? PlayerUxSettingsChanged;

    // Visibility
    Task ShowAsync();
    Task HideAsync();

    // Called by the component when the current track finishes playing
    Task OnTrackEndedAsync(CancellationToken cancellationToken = default);
}
