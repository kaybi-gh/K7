using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

/// <summary>
/// In-process registry of the cancellation sources of running background tasks.
/// </summary>
/// <remarks>
/// Cancelling only writes a status without this: the handler keeps running, keeps holding its lane slot
/// and keeps producing side effects. Like the concurrency gate, this is per process, which is consistent
/// with K7 running as a single instance.
/// </remarks>
public sealed class BackgroundTaskCancellationRegistry : IBackgroundTaskCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public void Register(Guid taskId, CancellationTokenSource cancellationTokenSource)
        => _sources[taskId] = cancellationTokenSource;

    public void Unregister(Guid taskId)
        => _sources.TryRemove(taskId, out _);

    public bool TryCancel(Guid taskId)
    {
        if (!_sources.TryGetValue(taskId, out var source))
            return false;

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // The task finished between the lookup and the cancel; nothing left to stop.
            return false;
        }
    }
}
