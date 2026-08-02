using K7.Server.Application.Common.Exceptions;
using K7.Server.Domain.Constants;
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

        if (ex is ProviderRateLimitedException rateLimited)
        {
            ScheduleRateLimitedRetry(task, rateLimited.RetryAfter);
            return;
        }

        var backoffSeconds = Math.Min(30 * Math.Pow(2, task.AttemptCount), maxBackoff.TotalSeconds);

        // Full jitter. Without it, every task that failed on the same provider outage retries at the
        // same instant and reproduces the outage as a self-inflicted burst.
        var jitteredSeconds = backoffSeconds * (0.5 + (Random.Shared.NextDouble() * 0.5));

        var delay = TimeSpan.FromSeconds(jitteredSeconds);
        task.Status = BackgroundTaskStatus.WaitingForRetry;
        task.NextRetryAfter = DateTimeOffset.UtcNow.Add(delay);
        task.StartedAt = null;
        task.CompletedAt = null;
    }

    /// <summary>
    /// Aligns retry with the provider Retry-After and boosts priority so this task is preferred when
    /// the provider cooldown elapses (other Pending work must not jump the queue and 429 again).
    /// </summary>
    public static void ScheduleRateLimitedRetry(
        BackgroundTask task,
        TimeSpan retryAfter,
        DateTimeOffset? utcNow = null)
    {
        var delay = retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(5);
        var now = utcNow ?? DateTimeOffset.UtcNow;

        task.Status = BackgroundTaskStatus.WaitingForRetry;
        task.NextRetryAfter = now.Add(delay);
        task.Priority = Math.Max(task.Priority, BackgroundTaskScheduling.OnDemandBoost);
        task.StartedAt = null;
        task.CompletedAt = null;
    }
}
