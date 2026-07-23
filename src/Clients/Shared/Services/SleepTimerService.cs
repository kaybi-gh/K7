using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Services;

public class SleepTimerService(IAudioPlayerService audioPlayerService) : ISleepTimerService, IDisposable
{
    private Timer? _timer;
    private Timer? _tickTimer;
    private DateTime _expiresAt;
    private int _durationElapsed;

    public bool IsActive { get; private set; }
    public SleepTimerMode Mode { get; private set; }

    public TimeSpan Remaining
    {
        get
        {
            if (!IsActive)
                return TimeSpan.Zero;

            if (Mode == SleepTimerMode.EndOfTrack)
            {
                var left = audioPlayerService.Duration - audioPlayerService.CurrentTime;
                return TimeSpan.FromSeconds(Math.Max(0, left));
            }

            if (Mode == SleepTimerMode.EndOfQueue)
                return TimeSpan.Zero;

            var remaining = _expiresAt - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public event Action? TimerChanged;
    public event Action? TimerExpired;

    public void Start(SleepTimerMode mode, TimeSpan? duration = null)
    {
        Cancel();

        Mode = mode;
        IsActive = true;
        _durationElapsed = 0;
        audioPlayerService.StopAfterCurrentTrackCompleted += OnStopAfterCurrentTrackCompleted;

        switch (mode)
        {
            case SleepTimerMode.Duration when duration.HasValue:
                _expiresAt = DateTime.UtcNow + duration.Value;
                _timer = new Timer(OnDurationElapsed, null, duration.Value, Timeout.InfiniteTimeSpan);
                _tickTimer = new Timer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                break;

            case SleepTimerMode.EndOfTrack:
                BeginFinishCurrentTrack();
                _tickTimer = new Timer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                break;

            case SleepTimerMode.EndOfQueue:
                audioPlayerService.PlaybackStateChanged += OnPlaybackStateChangedForSleep;
                _expiresAt = DateTime.MaxValue;
                break;

            default:
                IsActive = false;
                Mode = SleepTimerMode.Off;
                audioPlayerService.StopAfterCurrentTrackCompleted -= OnStopAfterCurrentTrackCompleted;
                return;
        }

        TimerChanged?.Invoke();
    }

    public void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
        _tickTimer?.Dispose();
        _tickTimer = null;
        audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChangedForSleep;
        audioPlayerService.StopAfterCurrentTrackCompleted -= OnStopAfterCurrentTrackCompleted;
        audioPlayerService.ClearStopAfterCurrentTrack();
        IsActive = false;
        Mode = SleepTimerMode.Off;
        _durationElapsed = 0;
        TimerChanged?.Invoke();
    }

    private void OnTick(object? state)
    {
        if (!IsActive)
            return;

        // Belt-and-suspenders: if the one-shot timer was delayed (doze / suspension),
        // still transition when the countdown reaches zero.
        if (Mode == SleepTimerMode.Duration && DateTime.UtcNow >= _expiresAt)
        {
            OnDurationElapsed(null);
            return;
        }

        TimerChanged?.Invoke();
    }

    private void OnDurationElapsed(object? state)
    {
        if (Interlocked.Exchange(ref _durationElapsed, 1) == 1)
            return;

        if (!IsActive || Mode != SleepTimerMode.Duration)
            return;

        _timer?.Dispose();
        _timer = null;

        // Duration reached: finish the current track (fade near the end), then pause.
        Mode = SleepTimerMode.EndOfTrack;
        BeginFinishCurrentTrack();
        TimerChanged?.Invoke();
    }

    private void BeginFinishCurrentTrack()
    {
        audioPlayerService.RequestStopAfterCurrentTrack();

        // Already idle / ended: stop immediately.
        if (audioPlayerService.PlaybackState is PlaybackState.Ended or PlaybackState.Idle)
        {
            audioPlayerService.Pause();
            CompleteExpired();
        }
    }

    private void OnStopAfterCurrentTrackCompleted()
    {
        if (!IsActive)
            return;

        CompleteExpired();
    }

    private void OnPlaybackStateChangedForSleep(PlaybackState state)
    {
        if (Mode == SleepTimerMode.EndOfQueue && state is PlaybackState.Idle or PlaybackState.Ended)
            CompleteExpired();
    }

    private void CompleteExpired()
    {
        Cancel();
        TimerExpired?.Invoke();
    }

    public void Dispose()
    {
        Cancel();
    }
}
