namespace K7.Server.Domain.Events;

public class BackgroundTaskDeletedEvent : BaseEvent
{
    public BackgroundTaskDeletedEvent(BackgroundTask backgroundTask)
    {
        BackgroundTask = backgroundTask;
    }

    public BackgroundTask BackgroundTask { get; }
}
