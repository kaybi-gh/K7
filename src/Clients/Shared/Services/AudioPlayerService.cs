using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Services;

public class AudioPlayerService(IStreamUriService streamUriService, IDeviceStorageService deviceStorageService) : IAudioPlayerService
{
    // Transport events
    public event Func<Task>? PlayRequested;
    public event Func<Task>? PauseRequested;
    public event Func<Task>? StopRequested;
    public event Func<double, Task>? SeekRequested;
    public event Func<Task>? MuteRequested;
    public event Func<Task>? UnmuteRequested;
    public event Func<double, Task>? VolumeChangeRequested;

    // State change events
#pragma warning disable CS0067
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
    public event Action? IsFullScreenVisibleChanged;
    public event Func<PlayerSource, double, Task>? CrossfadeRequested;
    public event Func<PlayerSource, Task>? GaplessPrebufferRequested;
    public event Func<double, Task>? FadeOutRequested;
    public event Func<Task>? FadeResetRequested;
#pragma warning restore CS0067
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<bool>? IsMutedChanged;

    // Queue events
    public event Action? QueueChanged;
    public event Action<AudioQueueItem?>? CurrentTrackChanged;
    public event Action<RepeatMode>? RepeatModeChanged;
    public event Action<bool>? ShuffleChanged;

    // Playback state backing fields
    private PlaybackState _playbackState = PlaybackState.Unknown;
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            if (_playbackState == value) return;
            // While buffering, ignore intermediate states (Paused/Idle) from the native player
            if (_playbackState == PlaybackState.Buffering && value is PlaybackState.Paused or PlaybackState.Idle)
                return;
            _playbackState = value;
            PlaybackStateChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Sets Idle even when currently Buffering (load/auth failures must not spin forever).
    /// </summary>
    public void ForceIdle()
    {
        if (_playbackState == PlaybackState.Idle)
            return;

        _playbackState = PlaybackState.Idle;
        PlaybackStateChanged?.Invoke(_playbackState);
    }

    public double Duration { get; set { if (field != value) { field = value; DurationChanged?.Invoke(value); } } }

    public double CurrentTime { get; set
        {
            // After the UI has flipped to the incoming track, ignore late ticks from the
            // outgoing element. While UI is deferred, keep reporting the outgoing clock.
            if (_crossfadeTriggered && !_crossfadeUiDeferred && value > 1)
                return;

            if (field == value) return;
            field = value;
            CurrentTimeChanged?.Invoke(value);
            ConsiderStopAfterTrackFade();
            ConsiderCrossfade();
            ConsiderGaplessPrebuffer();
        } }

    public double BufferedTime { get; set { if (field != value) { field = value; BufferedTimeChanged?.Invoke(value); } } }

    public double Volume { get; set
        {
            if (field != value)
            {
                field = value;
                if (!System.OperatingSystem.IsAndroid() && !System.OperatingSystem.IsIOS())
                    deviceStorageService.Set(PreferenceKeys.PLAYER_VOLUME, value);
                VolumeChanged?.Invoke(value);
            }
        } } = System.OperatingSystem.IsAndroid() || System.OperatingSystem.IsIOS()
        ? 1.0
        : deviceStorageService.Get(PreferenceKeys.PLAYER_VOLUME, 1);

    public bool IsMuted { get; set
        {
            if (field != value)
            {
                field = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_IS_MUTED, value);
                IsMutedChanged?.Invoke(value);
            }
        } } = deviceStorageService.Get(PreferenceKeys.PLAYER_IS_MUTED, false);

    public bool IsVisible { get; private set; }
    public bool IsFullScreenVisible { get; private set; }

    // Crossfade state
    public bool AdaptiveCrossfade { get; private set; } = deviceStorageService.Get(PreferenceKeys.PLAYER_ADAPTIVE_CROSSFADE, true);

    public event Action? CrossfadeDurationChanged;

    public double CrossfadeDuration { get; private set; } = deviceStorageService.Get(PreferenceKeys.PLAYER_CROSSFADE_DURATION, 6.0);

    /// <summary>
    /// Window used to trigger crossfade/gapless. Duration 0 always means gapless
    /// (adaptive only adjusts duration when the slider is greater than 0).
    /// </summary>
    public double CrossfadeTriggerWindow => CrossfadeDuration > 0 ? CrossfadeDuration : 0;

    private bool _crossfadeTriggered;
    private bool _crossfadeUiDeferred;
    /// <summary>
    /// Adaptive/gapless decided not to crossfade this track pair. Prevents re-arming
    /// on every timeupdate (which would drain shuffle via GetNextIndex).
    /// </summary>
    private bool _crossfadeDeclined;

    // Loudness normalization state
    public event Action? LoudnessSettingsChanged;

    public bool LoudnessEnabled { get; private set; } = deviceStorageService.Get(PreferenceKeys.LOUDNESS_ENABLED, true);
    public double LoudnessTargetLufs { get; private set; } = deviceStorageService.Get(PreferenceKeys.LOUDNESS_TARGET_LUFS, -14.0);
    public double LoudnessPreampDb { get; private set; } = deviceStorageService.Get(PreferenceKeys.LOUDNESS_PREAMP_DB, 0.0);
    public bool LimiterEnabled { get; private set; } = deviceStorageService.Get(PreferenceKeys.LOUDNESS_LIMITER_ENABLED, true);

    // EQ state
    public event Action? EqSettingsChanged;

    public bool EqEnabled { get; private set; } = deviceStorageService.Get(PreferenceKeys.EQ_ENABLED, false);
    public double[] EqBands { get; private set; } = ParseEqBands(deviceStorageService.Get(PreferenceKeys.EQ_BANDS_JSON, null));
    public string? EqPresetName { get; private set; } = deviceStorageService.Get(PreferenceKeys.EQ_PRESET_NAME, null);

    // Queue state
    private readonly List<AudioQueueItem> _queue = [];
    private readonly List<AudioQueueItem> _playHistory = [];
    private readonly List<int> _shuffleOrder = [];
    private int _shufflePosition = -1;
    private const int MaxPlayHistory = 50;

    public IReadOnlyList<AudioQueueItem> Queue => _queue;
    public IReadOnlyList<AudioQueueItem> PlayHistory => _playHistory;
    public AudioQueueItem? CurrentTrack => CurrentIndex >= 0 && CurrentIndex < _queue.Count ? _queue[CurrentIndex] : null;
    public int CurrentIndex { get; private set; } = -1;

    public RepeatMode Repeat { get; private set; } = RepeatMode.Off;

    public bool Shuffle { get; private set; }

    public string? ActiveRadioTitle { get; private set; }
    public Guid? ActivePlaylistId { get; private set; }
    public event Action? ActivePlaylistChanged;
    public event Action? ActiveRadioChanged;

    private static readonly Random Rng = new();

    // Transport controls
    public void Play() => PlayRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Seek(double time) => SeekRequested?.Invoke(time);

    public void Mute()
    {
        IsMuted = true;
        MuteRequested?.Invoke();
    }

    public void Unmute()
    {
        IsMuted = false;
        UnmuteRequested?.Invoke();
    }

    public void SetVolume(double volume)
    {
        // Persist / update Volume here. Native and Web handlers only apply output level;
        // without this, MAUI Windows keeps the default 1.0 and resets on each SourceChanged.
        var clamped = Math.Clamp(volume, 0.0, 1.0);
        Volume = clamped;
        VolumeChangeRequested?.Invoke(clamped);
    }

    public void Stop()
    {
        StopRequested?.Invoke();
        ForceIdle();
    }

    // Visibility
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

    public void ToggleFullScreen()
    {
        IsFullScreenVisible = !IsFullScreenVisible;
        IsFullScreenVisibleChanged?.Invoke();
    }

    // Player UX preferences
    public event Action? PlayerUxSettingsChanged;

    public bool ShowFullscreenOnPlay { get; private set; } = deviceStorageService.Get(PreferenceKeys.SHOW_FULLSCREEN_ON_PLAY, false);

    public int SkipBackSeconds { get; private set; } = deviceStorageService.Get(PreferenceKeys.SKIP_BACK_SECONDS, 5);
    public int SkipForwardSeconds { get; private set; } = deviceStorageService.Get(PreferenceKeys.SKIP_FORWARD_SECONDS, 5);

    public bool KeepScreenOn { get; private set; } = deviceStorageService.Get(PreferenceKeys.KEEP_SCREEN_ON, false);

    public void SetShowFullscreenOnPlay(bool enabled)
    {
        ShowFullscreenOnPlay = enabled;
        deviceStorageService.Set(PreferenceKeys.SHOW_FULLSCREEN_ON_PLAY, enabled);
        PlayerUxSettingsChanged?.Invoke();
    }

    public void SetSkipBackSeconds(int seconds)
    {
        SkipBackSeconds = seconds;
        deviceStorageService.Set(PreferenceKeys.SKIP_BACK_SECONDS, seconds);
        PlayerUxSettingsChanged?.Invoke();
    }

    public void SetSkipForwardSeconds(int seconds)
    {
        SkipForwardSeconds = seconds;
        deviceStorageService.Set(PreferenceKeys.SKIP_FORWARD_SECONDS, seconds);
        PlayerUxSettingsChanged?.Invoke();
    }

    public void SetKeepScreenOn(bool enabled)
    {
        KeepScreenOn = enabled;
        deviceStorageService.Set(PreferenceKeys.KEEP_SCREEN_ON, enabled);
        PlayerUxSettingsChanged?.Invoke();
    }

    // Queue management
    public async Task PlayTrackAsync(AudioQueueItem track, CancellationToken cancellationToken = default)
    {
        ClearRadioContext();
        ClearPlaylistContext();
        await LoadQueueAsync([track], 0, cancellationToken);
    }

    public async Task PlayTracksAsync(IEnumerable<AudioQueueItem> tracks, int startIndex = 0, Guid? playlistId = null, CancellationToken cancellationToken = default)
    {
        ClearRadioContext();
        SetPlaylistContext(playlistId);
        await LoadQueueAsync(tracks, startIndex, cancellationToken);
    }

    public async Task PlayShuffledAsync(IEnumerable<AudioQueueItem> tracks, Guid? playlistId = null, CancellationToken cancellationToken = default)
    {
        var list = tracks as IList<AudioQueueItem> ?? tracks.ToList();
        if (list.Count == 0)
            return;

        if (!Shuffle)
        {
            Shuffle = true;
            ShuffleChanged?.Invoke(Shuffle);
        }

        var startIndex = Rng.Next(list.Count);
        ClearRadioContext();
        SetPlaylistContext(playlistId);
        await LoadQueueAsync(list, startIndex, cancellationToken);
    }

    public async Task PlayRadioAsync(IEnumerable<AudioQueueItem> tracks, string radioTitle, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        ClearPlaylistContext();
        ActiveRadioTitle = radioTitle;
        ActiveRadioChanged?.Invoke();
        await LoadQueueAsync(tracks, startIndex, cancellationToken);
    }

    private async Task LoadQueueAsync(IEnumerable<AudioQueueItem> tracks, int startIndex, CancellationToken cancellationToken)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        _playHistory.Clear();
        CurrentIndex = startIndex;
        RebuildShuffleOrder();
        QueueChanged?.Invoke();

        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    private void ClearRadioContext()
    {
        if (ActiveRadioTitle is null)
            return;

        ActiveRadioTitle = null;
        ActiveRadioChanged?.Invoke();
    }

    private void SetPlaylistContext(Guid? playlistId)
    {
        if (ActivePlaylistId == playlistId)
            return;

        ActivePlaylistId = playlistId;
        ActivePlaylistChanged?.Invoke();
    }

    private void ClearPlaylistContext()
    {
        if (ActivePlaylistId is null)
            return;

        ActivePlaylistId = null;
        ActivePlaylistChanged?.Invoke();
    }

    public void AddToQueue(AudioQueueItem track) => AddToQueue([track]);

    public void AddToQueue(IReadOnlyList<AudioQueueItem> tracks)
    {
        if (tracks.Count == 0)
            return;

        foreach (var track in tracks)
        {
            _queue.Add(track);
            if (Shuffle)
                _shuffleOrder.Add(_queue.Count - 1);
        }

        QueueChanged?.Invoke();
    }

    public void AddToQueueNext(AudioQueueItem track)
    {
        var insertIndex = CurrentIndex + 1;
        _queue.Insert(insertIndex, track);

        // Fix shuffle order indices
        if (Shuffle)
        {
            for (var i = 0; i < _shuffleOrder.Count; i++)
            {
                if (_shuffleOrder[i] >= insertIndex)
                    _shuffleOrder[i]++;
            }
            _shuffleOrder.Insert(_shufflePosition + 1, insertIndex);
        }

        QueueChanged?.Invoke();
    }

    public void RemoveFromQueue(int index)
    {
        if (index < 0 || index >= _queue.Count) return;

        var wasCurrent = index == CurrentIndex;
        _queue.RemoveAt(index);

        if (Shuffle)
        {
            _shuffleOrder.Remove(index);
            for (var i = 0; i < _shuffleOrder.Count; i++)
            {
                if (_shuffleOrder[i] > index)
                    _shuffleOrder[i]--;
            }
        }

        if (CurrentIndex > index)
            CurrentIndex--;
        else if (CurrentIndex >= _queue.Count)
            CurrentIndex = _queue.Count - 1;

        QueueChanged?.Invoke();

        if (wasCurrent)
            CurrentTrackChanged?.Invoke(CurrentTrack);
    }

    public void ClearQueue()
    {
        ClearPlaylistContext();
        _queue.Clear();
        _playHistory.Clear();
        _shuffleOrder.Clear();
        CurrentIndex = -1;
        _shufflePosition = -1;
        QueueChanged?.Invoke();
        CurrentTrackChanged?.Invoke(null);
    }

    // Navigation
    public async Task SkipToIndexAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _queue.Count || index == CurrentIndex) return;
        PushCurrentToPlayHistory();
        CurrentIndex = index;
        if (Shuffle)
            _shufflePosition = _shuffleOrder.IndexOf(index);
        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0) return;

        var nextIndex = GetNextIndex();
        if (nextIndex is null)
        {
            PlaybackState = PlaybackState.Ended;
            return;
        }

        PushCurrentToPlayHistory();
        CurrentIndex = nextIndex.Value;
        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    public async Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0) return;

        // If more than 3s into the track, restart it
        if (CurrentTime > 3)
        {
            Seek(0);
            return;
        }

        var prevIndex = GetPreviousIndex();
        if (prevIndex is null)
        {
            Seek(0);
            return;
        }

        CurrentIndex = prevIndex.Value;
        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    public void SyncCurrentIndexFromExternalPlayer(int index)
    {
        if (index < 0 || index >= _queue.Count || index == CurrentIndex)
            return;

        PushCurrentToPlayHistory();
        CurrentIndex = index;
        if (Shuffle)
            _shufflePosition = _shuffleOrder.IndexOf(index);

        _crossfadeTriggered = false;
        _crossfadeUiDeferred = false;
        _crossfadeDeclined = false;
        _gaplessPrebufferTriggered = false;
        ClearPreparedNextSource();

        CurrentTime = 0;
        Duration = CurrentTrack?.Duration ?? 0;
        BufferedTime = 0;
        CurrentTrackChanged?.Invoke(CurrentTrack);
    }

    // Modes
    public void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        if (Shuffle)
            RebuildShuffleOrder();
        ShuffleChanged?.Invoke(Shuffle);
    }

    public void CycleRepeatMode()
    {
        Repeat = Repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off
        };
        RepeatModeChanged?.Invoke(Repeat);
    }

    public void ToggleAdaptiveCrossfade()
    {
        AdaptiveCrossfade = !AdaptiveCrossfade;
        deviceStorageService.Set(PreferenceKeys.PLAYER_ADAPTIVE_CROSSFADE, AdaptiveCrossfade);
        CrossfadeDurationChanged?.Invoke();
    }

    public void SetCrossfadeDuration(double seconds)
    {
        CrossfadeDuration = Math.Clamp(seconds, 0, 12);
        deviceStorageService.Set(PreferenceKeys.PLAYER_CROSSFADE_DURATION, CrossfadeDuration);
        CrossfadeDurationChanged?.Invoke();
    }

    public void SetLoudnessEnabled(bool enabled)
    {
        LoudnessEnabled = enabled;
        deviceStorageService.Set(PreferenceKeys.LOUDNESS_ENABLED, enabled);
        LoudnessSettingsChanged?.Invoke();
    }

    public void SetLoudnessTargetLufs(double lufs)
    {
        LoudnessTargetLufs = Math.Clamp(lufs, -26.0, -6.0);
        deviceStorageService.Set(PreferenceKeys.LOUDNESS_TARGET_LUFS, LoudnessTargetLufs);
        LoudnessSettingsChanged?.Invoke();
    }

    public void SetLoudnessPreampDb(double db)
    {
        LoudnessPreampDb = Math.Clamp(db, -6.0, 6.0);
        deviceStorageService.Set(PreferenceKeys.LOUDNESS_PREAMP_DB, LoudnessPreampDb);
        LoudnessSettingsChanged?.Invoke();
    }

    public void SetLimiterEnabled(bool enabled)
    {
        LimiterEnabled = enabled;
        deviceStorageService.Set(PreferenceKeys.LOUDNESS_LIMITER_ENABLED, enabled);
        LoudnessSettingsChanged?.Invoke();
    }

    public void SetEqEnabled(bool enabled)
    {
        EqEnabled = enabled;
        deviceStorageService.Set(PreferenceKeys.EQ_ENABLED, enabled);
        EqSettingsChanged?.Invoke();
    }

    public void SetEqBands(double[] bands)
    {
        EqBands = bands;
        deviceStorageService.Set(PreferenceKeys.EQ_BANDS_JSON, System.Text.Json.JsonSerializer.Serialize(bands));
        EqSettingsChanged?.Invoke();
    }

    public void SetEqPresetName(string? name)
    {
        EqPresetName = name;
        deviceStorageService.Set(PreferenceKeys.EQ_PRESET_NAME, name ?? string.Empty);
        EqSettingsChanged?.Invoke();
    }

    private static double[] ParseEqBands(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new double[10];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<double[]>(json) ?? new double[10];
        }
        catch
        {
            return new double[10];
        }
    }

    public async Task OnCrossfadeNeededAsync(CancellationToken cancellationToken = default)
    {
        if (StopAfterCurrentTrack) return;
        if (_crossfadeTriggered || _crossfadeDeclined || _queue.Count == 0) return;
        if (Repeat == RepeatMode.One) return;

        // Peek only - GetNextIndex mutates shuffle position and must not run until
        // we commit to a real crossfade (same-album adaptive returns 0 for gapless).
        var nextIndex = PeekNextIndex();
        if (nextIndex is null)
            return;

        var nextTrack = _queue[nextIndex.Value];

        var duration = CrossfadeDuration;
        var outgoingTrack = CurrentTrack;
        if (AdaptiveCrossfade && outgoingTrack is not null)
        {
            var baseDuration = CrossfadeDuration > 0 ? CrossfadeDuration : 6.0;
            duration = HarmonicMixHelper.ComputeCrossfadeDuration(outgoingTrack, nextTrack, baseDuration);
        }

        if (duration <= 0)
        {
            // Gapless / same album: let OnTrackEnded advance the queue normally.
            _crossfadeDeclined = true;
            return;
        }

        _crossfadeTriggered = true;

        // Reuse the early-prepared source when possible so JS can keep the already
        // buffered <audio> element (fresh stream sessions get new URLs).
        PlayerSource? source = null;
        if (_preparedNextSource is not null
            && _preparedNextIndexedFileId == nextTrack.IndexedFileId
            && !string.IsNullOrEmpty(_preparedNextSource.Url))
        {
            source = _preparedNextSource;
        }
        else if (!string.IsNullOrEmpty(nextTrack.LocalPath))
        {
            source = new PlayerSource
            {
                Url = nextTrack.LocalPath,
                MimeType = "audio/mpeg",
                IndexedFileId = nextTrack.IndexedFileId
            };
        }
        else
        {
            var session = await GetSessionForTrackAsync(nextTrack, cancellationToken);
            if (session?.Source is null)
            {
                _crossfadeTriggered = false;
                return;
            }

            source = new PlayerSource
            {
                Url = session.Source.Uri.OriginalString,
                MimeType = session.Source.MimeType,
                IndexedFileId = nextTrack.IndexedFileId
            };
        }

        ClearPreparedNextSource();

        // Commit queue advance only after the next source is ready.
        var committedIndex = GetNextIndex();
        if (committedIndex is null || committedIndex.Value != nextIndex.Value)
        {
            _crossfadeTriggered = false;
            return;
        }

        PushCurrentToPlayHistory();
        CurrentIndex = committedIndex.Value;
        // Keep seek bar / title / waveform on the outgoing track during the blend.
        // Flipping UI at arm-time feels like an early cut even when audio overlaps.
        _crossfadeUiDeferred = true;
        CrossfadeRequested?.Invoke(source, duration);
    }

    public void NotifyCrossfadeCompleted()
    {
        _crossfadeTriggered = false;
        if (!_crossfadeUiDeferred)
            return;

        _crossfadeUiDeferred = false;
        var track = CurrentTrack;
        if (Duration <= 0)
            Duration = track?.Duration ?? 0;
        // CurrentTime is already the incoming clock from JS; do not snap to 0 here.
        CurrentTrackChanged?.Invoke(track);
    }

    private void ConsiderCrossfade()
    {
        if (_crossfadeTriggered || _crossfadeDeclined || StopAfterCurrentTrack || CrossfadeTriggerWindow <= 0)
            return;
        if (_playbackState != PlaybackState.Playing || CurrentTime <= 0)
            return;

        // Prefer track metadata duration so a transient MediaElement Duration glitch
        // cannot fire a mid-track equal-power fade (sounds like unexplained cutouts).
        var duration = GetReliableDurationSeconds();
        if (duration <= 0)
            return;

        var remaining = duration - CurrentTime;
        // Arm a bit early once the next track is prebuffered so JS can start it
        // silently and still run a full-duration equal-power blend.
        var armWindow = CrossfadeTriggerWindow;
        if (_preparedNextSource is not null)
            armWindow += 2;
        if (remaining > 0 && remaining <= armWindow)
            _ = OnCrossfadeNeededAsync();
    }

    private void ConsiderGaplessPrebuffer()
    {
        if (_gaplessPrebufferTriggered || _crossfadeTriggered || StopAfterCurrentTrack)
            return;
        if (_playbackState != PlaybackState.Playing || CurrentTime <= 0)
            return;

        var duration = GetReliableDurationSeconds();
        if (duration <= 0)
            return;

        var remaining = duration - CurrentTime;
        // Prepare the next track early for gapless and crossfade so the incoming
        // stream is already decoding when the equal-power ramp starts.
        var prepareWindow = CrossfadeTriggerWindow > 0
            ? CrossfadeTriggerWindow + 10
            : 10;
        if (remaining > 0 && remaining <= prepareWindow)
            _ = OnGaplessPrebufferNeededAsync();
    }

    private double GetReliableDurationSeconds()
    {
        var trackDuration = CurrentTrack?.Duration ?? 0;
        if (trackDuration > 0 && Duration > 0)
        {
            // If native duration is wildly shorter than metadata, trust metadata
            // (avoids early crossfade / false "end of track" fades).
            if (Duration < trackDuration * 0.5)
                return trackDuration;
            return Math.Max(Duration, trackDuration * 0.95);
        }

        return trackDuration > 0 ? trackDuration : Duration;
    }

    private bool _gaplessPrebufferTriggered;
    private PlayerSource? _preparedNextSource;
    private Guid? _preparedNextIndexedFileId;

    private void ClearPreparedNextSource()
    {
        _preparedNextSource = null;
        _preparedNextIndexedFileId = null;
    }

    public async Task OnGaplessPrebufferNeededAsync(CancellationToken cancellationToken = default)
    {
        if (StopAfterCurrentTrack) return;
        if (_gaplessPrebufferTriggered || _crossfadeTriggered || _queue.Count == 0) return;
        if (Repeat == RepeatMode.One) return;

        var nextIndex = PeekNextIndex();
        if (nextIndex is null) return;

        var nextTrack = _queue[nextIndex.Value];

        PlayerSource source;
        if (!string.IsNullOrEmpty(nextTrack.LocalPath))
        {
            source = new PlayerSource
            {
                Url = nextTrack.LocalPath,
                MimeType = "audio/mpeg",
                IndexedFileId = nextTrack.IndexedFileId
            };
        }
        else
        {
            var session = await GetSessionForTrackAsync(nextTrack, cancellationToken);
            if (session?.Source is null) return;
            source = new PlayerSource
            {
                Url = session.Source.Uri.OriginalString,
                MimeType = session.Source.MimeType,
                IndexedFileId = nextTrack.IndexedFileId
            };
        }

        _gaplessPrebufferTriggered = true;
        _preparedNextSource = source;
        _preparedNextIndexedFileId = nextTrack.IndexedFileId;
        GaplessPrebufferRequested?.Invoke(source);
    }

    // Called by the component when JS reports track ended
    public async Task OnTrackEndedAsync(CancellationToken cancellationToken = default)
    {
        if (StopAfterCurrentTrack)
        {
            await FinishStopAfterCurrentTrackAsync();
            return;
        }

        if (Repeat == RepeatMode.One)
        {
            Seek(0);
            Play();
            return;
        }

        if (_crossfadeTriggered)
        {
            // Finish deferred UI handoff even if the audible blend never completed.
            NotifyCrossfadeCompleted();
            // OnCrossfadeNeeded already advanced CurrentIndex. If the handoff never
            // reached a playable state (slow HLS resolve, failed promote), recover.
            if (PlaybackState is not (PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Paused))
                await LoadAndPlayCurrentAsync(cancellationToken);
            return;
        }

        await NextAsync(cancellationToken);
    }

    public async Task RecoverAfterNativePlaybackFailureAsync(CancellationToken cancellationToken = default)
    {
        _crossfadeTriggered = false;
        _crossfadeDeclined = false;
        _gaplessPrebufferTriggered = false;

        if (CurrentTrack is null)
        {
            PlaybackState = PlaybackState.Idle;
            return;
        }

        // Fresh stream session + SourceChanged so native players leave a broken Source behind.
        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    private bool _sleepFadeStarted;
    private const double SleepFadeSeconds = 8.0;

    public bool StopAfterCurrentTrack { get; private set; }
    public event Action? StopAfterCurrentTrackCompleted;

    public void RequestStopAfterCurrentTrack()
    {
        StopAfterCurrentTrack = true;
        _sleepFadeStarted = false;
        ConsiderStopAfterTrackFade();
    }

    public void ClearStopAfterCurrentTrack()
    {
        StopAfterCurrentTrack = false;
        _sleepFadeStarted = false;
    }

    private void ConsiderStopAfterTrackFade()
    {
        if (!StopAfterCurrentTrack || _sleepFadeStarted)
            return;

        var duration = GetReliableDurationSeconds();
        if (duration <= 0)
            return;

        var remaining = duration - CurrentTime;
        if (remaining <= 0 || remaining > SleepFadeSeconds)
            return;

        _sleepFadeStarted = true;
        _ = StartSleepFadeAsync(remaining);
    }

    private async Task StartSleepFadeAsync(double durationSeconds)
    {
        try
        {
            if (FadeOutRequested is not null)
                await FadeOutRequested.Invoke(Math.Max(0.25, durationSeconds));
        }
        catch
        {
            // Platform fade is best-effort; stop-after-track still pauses at end.
        }
    }

    private async Task FinishStopAfterCurrentTrackAsync()
    {
        StopAfterCurrentTrack = false;
        _sleepFadeStarted = false;
        Pause();
        PlaybackState = PlaybackState.Ended;

        try
        {
            if (FadeResetRequested is not null)
                await FadeResetRequested.Invoke();
        }
        catch
        {
            // ignore
        }

        StopAfterCurrentTrackCompleted?.Invoke();
    }

    // Private helpers
    private async Task<StreamingSessionDto?> GetSessionForTrackAsync(AudioQueueItem track, CancellationToken cancellationToken)
    {
        if (track.RemoteIndexedFileId is { } remoteFileId)
            return await streamUriService.GetOrCreateRemoteSessionAsync(remoteFileId, cancellationToken: cancellationToken);

        return await streamUriService.GetOrCreateSessionAsync(track.IndexedFileId, cancellationToken: cancellationToken);
    }

    private void PushCurrentToPlayHistory()
    {
        var track = CurrentTrack;
        if (track is null)
            return;

        _playHistory.Add(track);
        while (_playHistory.Count > MaxPlayHistory)
            _playHistory.RemoveAt(0);
    }

    private async Task LoadAndPlayCurrentAsync(CancellationToken cancellationToken)
    {
        var track = CurrentTrack;
        if (track is null) return;

        _crossfadeTriggered = false;
        _crossfadeUiDeferred = false;
        _crossfadeDeclined = false;
        _gaplessPrebufferTriggered = false;
        ClearPreparedNextSource();
        PlaybackState = PlaybackState.Buffering;

        // Reset before CurrentTrackChanged so the waveform UI paints 0:00 immediately.
        CurrentTime = 0;
        Duration = track.Duration ?? 0;
        BufferedTime = 0;
        CurrentTrackChanged?.Invoke(track);

        await ShowAsync();

        if (ShowFullscreenOnPlay && !IsFullScreenVisible)
            ToggleFullScreen();

        PlayerSource source;

        if (!string.IsNullOrEmpty(track.LocalPath))
        {
            source = new PlayerSource
            {
                Url = track.LocalPath,
                MimeType = "audio/mpeg"
            };
        }
        else
        {
            try
            {
                var session = await GetSessionForTrackAsync(track, cancellationToken);

                if (session?.Source is null)
                {
                    ForceIdle();
                    return;
                }

                source = new PlayerSource
                {
                    StreamSessionId = session.Id,
                    Url = session.Source.Uri.OriginalString,
                    MimeType = session.Source.MimeType
                };
            }
            catch (HttpRequestException)
            {
                // Auth refresh races / transient network should not crash the UI via ErrorBoundary.
                ForceIdle();
                return;
            }
        }

        SourceChanged?.Invoke(source);
    }

    private int? GetNextIndex()
    {
        if (Shuffle)
        {
            _shufflePosition++;
            if (_shufflePosition < _shuffleOrder.Count)
                return _shuffleOrder[_shufflePosition];

            if (Repeat == RepeatMode.All)
            {
                RebuildShuffleOrder();
                return _shuffleOrder.Count > 0 ? _shuffleOrder[0] : null;
            }

            return null;
        }

        var next = CurrentIndex + 1;
        if (next < _queue.Count)
            return next;

        if (Repeat == RepeatMode.All)
            return 0;

        return null;
    }

    private int? PeekNextIndex()
    {
        if (Shuffle)
        {
            var nextPos = _shufflePosition + 1;
            if (nextPos < _shuffleOrder.Count)
                return _shuffleOrder[nextPos];
            if (Repeat == RepeatMode.All && _shuffleOrder.Count > 0)
                return _shuffleOrder[0];
            return null;
        }

        var next = CurrentIndex + 1;
        if (next < _queue.Count)
            return next;

        if (Repeat == RepeatMode.All)
            return 0;

        return null;
    }

    private int? GetPreviousIndex()
    {
        if (Shuffle)
        {
            if (_shufflePosition > 0)
            {
                _shufflePosition--;
                return _shuffleOrder[_shufflePosition];
            }
            return null;
        }

        var prev = CurrentIndex - 1;
        return prev >= 0 ? prev : null;
    }

    private void RebuildShuffleOrder()
    {
        _shuffleOrder.Clear();
        var indices = new List<int>();
        for (var i = 0; i < _queue.Count; i++)
        {
            if (i != CurrentIndex)
                indices.Add(i);
        }

        // Fisher-Yates shuffle
        for (var i = indices.Count - 1; i > 0; i--)
        {
            var j = Rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        // Anti-repetition: reorder to avoid same artist back-to-back
        var result = new List<int>(indices.Count);
        var remaining = new List<int>(indices);

        string? lastArtist = CurrentIndex >= 0 && CurrentIndex < _queue.Count
            ? _queue[CurrentIndex].Artist
            : null;

        while (remaining.Count > 0)
        {
            var picked = -1;
            for (var i = 0; i < remaining.Count; i++)
            {
                var candidate = _queue[remaining[i]];
                if (candidate.Artist != lastArtist || string.IsNullOrEmpty(lastArtist))
                {
                    picked = i;
                    break;
                }
            }

            if (picked < 0)
                picked = 0;

            var idx = remaining[picked];
            result.Add(idx);
            lastArtist = _queue[idx].Artist;
            remaining.RemoveAt(picked);
        }

        _shuffleOrder.AddRange(result);
        _shufflePosition = -1;
    }
}
