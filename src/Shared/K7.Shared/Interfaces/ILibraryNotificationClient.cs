namespace K7.Shared.Interfaces;

public interface ILibraryNotificationClient
{
    Task ReceiveMediaAdded(Guid mediaId, string? title, string mediaType);
    Task ReceiveLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount);
}
