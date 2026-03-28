namespace K7.Server.Application.Common.Interfaces;

public interface IBackgroundTaskQueue
{
    void Enqueue(Guid taskId);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
