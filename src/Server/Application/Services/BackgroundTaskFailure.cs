using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

public static class BackgroundTaskFailure
{
    public static void MarkFailed(BackgroundTask task) =>
        MarkTerminal(task, BackgroundTaskStatus.Failed);

    public static void MarkCancelled(BackgroundTask task) =>
        MarkTerminal(task, BackgroundTaskStatus.Cancelled);

    private static void MarkTerminal(BackgroundTask task, BackgroundTaskStatus status)
    {
        task.Status = status;
        task.CompletedAt = DateTimeOffset.UtcNow;
        task.NextRetryAfter = null;
        task.StartedAt = null;
    }

    public static void Handle(BackgroundTask task, Exception ex, TimeSpan maxBackoff)
    {
        if (task.AttemptCount + 1 >= task.MaxAttempts)
        {
            MarkFailed(task);
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, task.AttemptCount), maxBackoff.TotalSeconds));
        task.Status = BackgroundTaskStatus.WaitingForRetry;
        task.NextRetryAfter = DateTimeOffset.UtcNow.Add(delay);
        task.StartedAt = null;
        task.CompletedAt = null;
    }
}
