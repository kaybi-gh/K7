using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Clients.Web.Services;

public class PlayerService(IDeviceStorageService deviceStorageService) : IPlayerService
{
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

#pragma warning disable CS0067
    public event Action<PlayerSource>? SourceChanged;
    public event Action? IsVisibleChanged;
#pragma warning restore CS0067
    public event Action<bool>? IsFullScreenChanged;
    public event Action<PlaybackState>? PlaybackStateChanged;
    public event Action<double>? DurationChanged;
    public event Action<double>? CurrentTimeChanged;
    public event Action<double>? BufferedTimeChanged;
    public event Action<double>? VolumeChanged;
    public event Action<double>? PlaybackRateChanged;
    public event Action<bool>? IsMutedChanged;

    private PlayerSource _source = new();
    public PlayerSource Source
    {
        get => _source;
        set
        {
            if (_source != value)
            {
                _source = value;
                SourceChanged?.Invoke(value);
            }
        }
    }

    public bool IsVisible { get; private set; } = false;


    private PlaybackState _playbackState = PlaybackState.Unknown;
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            if (_playbackState != value)
            {
                _playbackState = value;
                PlaybackStateChanged?.Invoke(value);
            }
        }
    }

    private bool _isFullScreen = false;
    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (_isFullScreen != value)
            {
                _isFullScreen = value;
                IsFullScreenChanged?.Invoke(value);
            }
        }
    }

    private double _duration = 0;
    public double Duration
    {
        get => _duration;
        set
        {
            if (_duration != value)
            {
                _duration = value;
                DurationChanged?.Invoke(value);
            }
        }
    }

    private double _currentTime = 0;
    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime != value)
            {
                _currentTime = value;
                CurrentTimeChanged?.Invoke(value);
            }
        }
    }

    private double _bufferedTime = 0;
    public double BufferedTime
    {
        get => _bufferedTime;
        set
        {
            if (_bufferedTime != value)
            {
                _bufferedTime = value;
                BufferedTimeChanged?.Invoke(value);
            }
        }
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

    private double _playbackRate = deviceStorageService.Get(PreferenceKeys.PLAYER_PLAYBACK_RATE, 1);
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate != value)
            {
                _playbackRate = value;
                deviceStorageService.Set(PreferenceKeys.PLAYER_PLAYBACK_RATE, value);
                PlaybackRateChanged?.Invoke(value);
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

    public void Play() => PlayRequested?.Invoke();
    public void Pause() => PauseRequested?.Invoke();
    public void Seek(double time) => SeekRequested?.Invoke(time);
    public void Mute() => MuteRequested?.Invoke();
    public void Unmute() => UnmuteRequest?.Invoke();
    public void SetVolume(double volume) => VolumeChangeRequested?.Invoke(volume);
    public void SetPlaybackRate(double rate) => PlaybackRateChangeRequested?.Invoke(rate);
    public void Stop() => StopRequested?.Invoke();
    public void EnterFullScreen() => EnterFullScreenRequested?.Invoke();
    public void ExitFullScreen() => ExitFullScreenRequested?.Invoke();
}
