using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Helpers;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger? logger = null, string? failureMessage = null)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is null) return;

            var ex = t.Exception.GetBaseException();
            if (IsBenignJsInteropFailure(ex))
            {
                logger?.LogDebug(ex, failureMessage ?? "Background task skipped during static render");
                return;
            }

            logger?.LogError(ex, failureMessage ?? "Background task failed");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static bool IsBenignJsInteropFailure(Exception ex) =>
        ex is JSDisconnectedException
        || (ex is InvalidOperationException
            && ex.Message.Contains("JavaScript interop", StringComparison.Ordinal));
}
