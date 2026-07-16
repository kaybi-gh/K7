using K7.Server.Application.Common.Interfaces;

namespace K7.Tests.Helpers;

/// <summary>
/// Drops enqueued work so FileIndexer functional tests are not raced by BackgroundTasksProcessingService.
/// </summary>
public sealed class NoOpBackgroundTaskQueue : IBackgroundTaskQueue
{
    public void Enqueue(Guid taskId)
    {
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => new(new TaskCompletionSource<Guid>().Task.WaitAsync(cancellationToken));
}
