namespace K7.Server.Domain.Events;

public class LibraryFilesIndexTriggeredEvent : BaseEvent
{
    public LibraryFilesIndexTriggeredEvent(Library library)
    {
        Library = library;
    }

    public Library Library { get; }
}
