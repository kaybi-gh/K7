using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Services;

public class SleepTimerService(IAudioPlayerService audioPlayerService) : ISleepTimerService, IDisposable
{
    private Timer? _timer;
    private DateTime _expiresAt;

    public bool IsActive { get; private set; }
    public SleepTimerMode Mode { get; private set; }
    public TimeSpan Remaining => IsActive ? _expiresAt - DateTime.UtcNow : TimeSpan.Zero;

    public event Action? TimerChanged;
    public event Action? TimerExpired;

    public void Start(SleepTimerMode mode, TimeSpan? duration = null)
    {
        Cancel();

        Mode = mode;
        IsActive = true;

        switch (mode)
        {
            case SleepTimerMode.Duration when duration.HasValue:
                _expiresAt = DateTime.UtcNow + duration.Value;
                _timer = new Timer(OnTimerElapsed, null, duration.Value, Timeout.InfiniteTimeSpan);
                break;

            case SleepTimerMode.EndOfTrack:
                audioPlayerService.CurrentTrackChanged += OnTrackChangedForSleep;
                _expiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(
                    Math.Max(audioPlayerService.Duration - audioPlayerService.CurrentTime, 0));
                break;

            case SleepTimerMode.EndOfQueue:
                // Will be handled by monitoring track end when queue is exhausted
                audioPlayerService.PlaybackStateChanged += OnPlaybackStateChangedForSleep;
                _expiresAt = DateTime.MaxValue;
                break;

            default:
                IsActive = false;
                return;
        }

        TimerChanged?.Invoke();
    }

    public void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
        audioPlayerService.CurrentTrackChanged -= OnTrackChangedForSleep;
        audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChangedForSleep;
        IsActive = false;
        Mode = SleepTimerMode.Off;
        TimerChanged?.Invoke();
    }

    private void OnTimerElapsed(object? state)
    {
        ExpireTimer();
    }

    private void OnTrackChangedForSleep(AudioQueueItem? _)
    {
        // EndOfTrack mode: pause when current track changes (i.e. track ended)
        if (Mode == SleepTimerMode.EndOfTrack)
            ExpireTimer();
    }

    private void OnPlaybackStateChangedForSleep(PlaybackState state)
    {
        // EndOfQueue mode: when playback stops naturally (queue exhausted)
        if (Mode == SleepTimerMode.EndOfQueue && state == PlaybackState.Idle)
            ExpireTimer();
    }

    private void ExpireTimer()
    {
        audioPlayerService.Pause();
        Cancel();
        TimerExpired?.Invoke();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        audioPlayerService.CurrentTrackChanged -= OnTrackChangedForSleep;
        audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChangedForSleep;
    }
}
