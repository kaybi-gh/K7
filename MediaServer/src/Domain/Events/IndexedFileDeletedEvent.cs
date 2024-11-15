namespace MediaServer.Domain.Events;

public class IndexedFileDeletedEvent : BaseEvent
{
    public IndexedFileDeletedEvent(IndexedFile indexedFile)
    {
        IndexedFile = indexedFile;
    }

    public IndexedFile IndexedFile { get; }
}
