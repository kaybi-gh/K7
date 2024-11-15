namespace MediaServer.Domain.Events;

public class BackgroundTaskCreatedEvent : BaseEvent
{
    public BackgroundTaskCreatedEvent(BackgroundTask backgroundTask)
    {
        BackgroundTask = backgroundTask;
    }

    public BackgroundTask BackgroundTask { get; }
}
