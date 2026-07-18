namespace K7.Server.Domain.Events;

public class LibraryScanCompletedEvent : BaseEvent
{
    public LibraryScanCompletedEvent(Library library, int addedCount, int skippedCount, int inaccessibleCount)
    {
        Library = library;
        AddedCount = addedCount;
        SkippedCount = skippedCount;
        InaccessibleCount = inaccessibleCount;
    }

    public Library Library { get; }
    public int AddedCount { get; }
    public int SkippedCount { get; }
    public int InaccessibleCount { get; }
}
