namespace K7.Shared.Interfaces;

public interface IBackgroundTaskNotificationClient
{
    Task ReceiveBackgroundTaskUpdated();
}
