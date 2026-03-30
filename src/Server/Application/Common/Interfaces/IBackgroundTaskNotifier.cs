namespace K7.Server.Application.Common.Interfaces;

public interface IBackgroundTaskNotifier
{
    Task NotifyBackgroundTaskUpdatedAsync(CancellationToken cancellationToken = default);
}
