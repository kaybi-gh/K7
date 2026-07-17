using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Helpers;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger? logger = null, string? failureMessage = null)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is null) return;

            logger?.LogError(t.Exception.GetBaseException(), failureMessage ?? "Background task failed");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
