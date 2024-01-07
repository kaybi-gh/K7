namespace MediaServer.Domain.Events;

public class LibraryDeletedEvent : BaseEvent
{
    public LibraryDeletedEvent(Library library)
    {
        Library = library;
    }

    public Library Library { get; }
}
