using K7.Shared.Dtos.Notifications;

namespace K7.Shared.Interfaces;

public interface ILibraryNotificationClient
{
    Task ReceiveMediaAdded(Guid mediaId, string? title, string mediaType);
    Task ReceiveMediaBatchAdded(List<MediaBatchItem> items);
    Task ReceiveMediaMetadataRefreshed(Guid mediaId);
    Task ReceiveMediaPicturesUpdated(Guid mediaId);
    Task ReceiveMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId);
    Task ReceiveLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount);
    Task ReceiveLibraryScanProgress(Guid libraryId, int processed, int total, string phase);
}
