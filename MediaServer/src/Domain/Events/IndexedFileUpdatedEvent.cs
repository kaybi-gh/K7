namespace MediaServer.Domain.Events;

public class IndexedFileUpdatedEvent : BaseEvent
{
    public IndexedFileUpdatedEvent(IndexedFile indexedFile)
    {
        IndexedFile = indexedFile;
    }

    public IndexedFile IndexedFile { get; }
}
