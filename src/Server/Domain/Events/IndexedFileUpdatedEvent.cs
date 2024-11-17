namespace K7.Server.Domain.Events;

public class IndexedFileUpdatedEvent : BaseEvent
{
    public IndexedFileUpdatedEvent(IndexedFile indexedFile)
    {
        IndexedFile = indexedFile;
    }

    public IndexedFile IndexedFile { get; }
}
