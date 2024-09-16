namespace MediaServer.Domain.Events;

public class IndexedFileCreatedEvent : BaseEvent
{
    public IndexedFileCreatedEvent(IndexedFile indexedFile, FileType fileType)
    {
        IndexedFile = indexedFile;
        FileType = fileType;
    }

    public IndexedFile IndexedFile { get; }
    public FileType FileType { get; }
}
