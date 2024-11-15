namespace MediaServer.Domain.Events;

public class LibraryFilesIndexTriggeredEvent : BaseEvent
{
    public LibraryFilesIndexTriggeredEvent(Library library)
    {
        Library = library;
    }

    public Library Library { get; }
}
