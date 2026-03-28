namespace K7.Server.Domain.Enums;

public enum BackgroundTaskStatus
{
    Pending,
    InProgress,
    WaitingForRetry,
    Completed,
    Failed
}
