namespace K7.Clients.Shared.Interfaces;

public interface ISleepTimerService
{
    bool IsActive { get; }
    SleepTimerMode Mode { get; }
    TimeSpan Remaining { get; }

    event Action? TimerChanged;
    event Action? TimerExpired;

    void Start(SleepTimerMode mode, TimeSpan? duration = null);
    void Cancel();
}

public enum SleepTimerMode
{
    Off,
    Duration,
    EndOfTrack,
    EndOfQueue
}
