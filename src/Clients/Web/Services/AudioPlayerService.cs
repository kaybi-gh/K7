using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Clients.Web.Services;

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
        set { if (_playbackState != value) { _playbackState = value; PlaybackStateChanged?.Invoke(value); } }
    }

    private double _duration;
    public double Duration
    {
        get => _duration;
        set { if (_duration != value) { _duration = value; DurationChanged?.Invoke(value); } }
    }

    private double _currentTime;
    public double CurrentTime
    {
        get => _currentTime;
        set { if (_currentTime != value) { _currentTime = value; CurrentTimeChanged?.Invoke(value); } }
    }

    private double _bufferedTime;
    public double BufferedTime
    {
        get => _bufferedTime;
        set { if (_bufferedTime != value) { _bufferedTime = value; BufferedTimeChanged?.Invoke(value); } }
    }

    private double _volume = deviceStorageService.Get(PreferenceKeys.PLAYER_VOLUME, 1);
    public double Volume
    {
        get => _volume;
        set
        {
            if (_volume != value)
            {
                _volume = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_VOLUME, value);
                VolumeChanged?.Invoke(value);
            }
        }
    }

    private bool _isMuted = deviceStorageService.Get(PreferenceKeys.PLAYER_IS_MUTED, false);
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_IS_MUTED, value);
                IsMutedChanged?.Invoke(value);
            }
        }
    }

    public bool IsVisible { get; private set; }

    // Queue state
    private readonly List<AudioQueueItem> _queue = [];
    private readonly List<int> _shuffleOrder = [];
    private int _currentIndex = -1;
    private int _shufflePosition = -1;

    public IReadOnlyList<AudioQueueItem> Queue => _queue;
    public AudioQueueItem? CurrentTrack => _currentIndex >= 0 && _currentIndex < _queue.Count ? _queue[_currentIndex] : null;
    public int CurrentIndex => _currentIndex;

    private RepeatMode _repeat = RepeatMode.Off;
    public RepeatMode Repeat => _repeat;

    private bool _shuffle;
    public bool Shuffle => _shuffle;

    private static readonly Random Rng = new();

    // Transport controls
    public void Play() => PlayRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Seek(double time) => SeekRequested?.Invoke(time);
    public void Mute() => MuteRequested?.Invoke();
    public void Unmute() => UnmuteRequested?.Invoke();
    public void SetVolume(double volume) => VolumeChangeRequested?.Invoke(volume);

    public void Stop()
    {
        StopRequested?.Invoke();
        PlaybackState = PlaybackState.Idle;
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

    // Queue management
    public async Task PlayTrackAsync(AudioQueueItem track, CancellationToken cancellationToken = default)
    {
        _queue.Clear();
        _queue.Add(track);
        _currentIndex = 0;
        RebuildShuffleOrder();
        QueueChanged?.Invoke();

        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    public async Task PlayTracksAsync(IEnumerable<AudioQueueItem> tracks, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        _currentIndex = startIndex;
        RebuildShuffleOrder();
        QueueChanged?.Invoke();

        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    public void AddToQueue(AudioQueueItem track)
    {
        _queue.Add(track);
        if (_shuffle)
            _shuffleOrder.Add(_queue.Count - 1);
        QueueChanged?.Invoke();
    }

    public void AddToQueueNext(AudioQueueItem track)
    {
        var insertIndex = _currentIndex + 1;
        _queue.Insert(insertIndex, track);

        // Fix shuffle order indices
        if (_shuffle)
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

        var wasCurrent = index == _currentIndex;
        _queue.RemoveAt(index);

        if (_shuffle)
        {
            _shuffleOrder.Remove(index);
            for (var i = 0; i < _shuffleOrder.Count; i++)
            {
                if (_shuffleOrder[i] > index)
                    _shuffleOrder[i]--;
            }
        }

        if (_currentIndex > index)
            _currentIndex--;
        else if (_currentIndex >= _queue.Count)
            _currentIndex = _queue.Count - 1;

        QueueChanged?.Invoke();

        if (wasCurrent)
            CurrentTrackChanged?.Invoke(CurrentTrack);
    }

    public void ClearQueue()
    {
        _queue.Clear();
        _shuffleOrder.Clear();
        _currentIndex = -1;
        _shufflePosition = -1;
        QueueChanged?.Invoke();
        CurrentTrackChanged?.Invoke(null);
    }

    // Navigation
    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0) return;

        var nextIndex = GetNextIndex();
        if (nextIndex is null)
        {
            PlaybackState = PlaybackState.Ended;
            return;
        }

        _currentIndex = nextIndex.Value;
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

        _currentIndex = prevIndex.Value;
        await LoadAndPlayCurrentAsync(cancellationToken);
    }

    // Modes
    public void ToggleShuffle()
    {
        _shuffle = !_shuffle;
        if (_shuffle)
            RebuildShuffleOrder();
        ShuffleChanged?.Invoke(_shuffle);
    }

    public void CycleRepeatMode()
    {
        _repeat = _repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off
        };
        RepeatModeChanged?.Invoke(_repeat);
    }

    // Called by the component when JS reports track ended
    public async Task OnTrackEndedAsync(CancellationToken cancellationToken = default)
    {
        if (_repeat == RepeatMode.One)
        {
            Seek(0);
            Play();
            return;
        }

        await NextAsync(cancellationToken);
    }

    // Private helpers
    private async Task LoadAndPlayCurrentAsync(CancellationToken cancellationToken)
    {
        var track = CurrentTrack;
        if (track is null) return;

        CurrentTrackChanged?.Invoke(track);

        // Reset state
        CurrentTime = 0;
        Duration = 0;
        BufferedTime = 0;
        PlaybackState = PlaybackState.Idle;

        await ShowAsync();
        Play();

        var session = await streamUriService.GetOrCreateSessionAsync(track.IndexedFileId, cancellationToken: cancellationToken);

        if (session.Source is null)
            throw new InvalidOperationException("Streaming session did not return a source URI.");

        var source = new PlayerSource
        {
            Url = session.Source.Uri.OriginalString,
            MimeType = session.Source.MimeType
        };

        SourceChanged?.Invoke(source);
    }

    private int? GetNextIndex()
    {
        if (_shuffle)
        {
            _shufflePosition++;
            if (_shufflePosition < _shuffleOrder.Count)
                return _shuffleOrder[_shufflePosition];

            if (_repeat == RepeatMode.All)
            {
                RebuildShuffleOrder();
                return _shuffleOrder.Count > 0 ? _shuffleOrder[0] : null;
            }

            return null;
        }

        var next = _currentIndex + 1;
        if (next < _queue.Count)
            return next;

        if (_repeat == RepeatMode.All)
            return 0;

        return null;
    }

    private int? GetPreviousIndex()
    {
        if (_shuffle)
        {
            if (_shufflePosition > 0)
            {
                _shufflePosition--;
                return _shuffleOrder[_shufflePosition];
            }
            return null;
        }

        var prev = _currentIndex - 1;
        return prev >= 0 ? prev : null;
    }

    private void RebuildShuffleOrder()
    {
        _shuffleOrder.Clear();
        for (var i = 0; i < _queue.Count; i++)
        {
            if (i != _currentIndex)
                _shuffleOrder.Add(i);
        }

        // Fisher-Yates shuffle
        for (var i = _shuffleOrder.Count - 1; i > 0; i--)
        {
            var j = Rng.Next(i + 1);
            (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
        }

        _shufflePosition = -1;
    }
}
