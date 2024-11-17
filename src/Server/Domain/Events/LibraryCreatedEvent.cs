namespace K7.Server.Domain.Events;

public class LibraryCreatedEvent : BaseEvent
{
    public LibraryCreatedEvent(Library library)
    {
        Library = library;
    }

    public Library Library { get; }
}
