using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Events;

public class IndexedFileDeletedEvent : BaseEvent
{
    public IndexedFileDeletedEvent(IndexedFile indexedFile, Guid? formerMediaId, Guid libraryId)
    {
        IndexedFile = indexedFile;
        FormerMediaId = formerMediaId;
        LibraryId = libraryId;
    }

    public IndexedFile IndexedFile { get; }
    public Guid? FormerMediaId { get; }
    public Guid LibraryId { get; }
}
