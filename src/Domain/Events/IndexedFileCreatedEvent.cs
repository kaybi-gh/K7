namespace MediaServer.Domain.Events;

public class IndexedFileCreatedEvent : BaseEvent
{
    public IndexedFileCreatedEvent(IndexedFile indexedFile)
    {
        IndexedFile = indexedFile;
    }

    public IndexedFile IndexedFile { get; }
}
