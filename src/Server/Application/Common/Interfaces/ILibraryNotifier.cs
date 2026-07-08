using K7.Shared.Dtos.Notifications;

namespace K7.Server.Application.Common.Interfaces;

public interface ILibraryNotifier
{
    Task NotifyMediaAddedAsync(Guid mediaId, string? title, string mediaType, CancellationToken cancellationToken = default);
    Task NotifyMediaBatchAddedAsync(List<MediaBatchItem> items, CancellationToken cancellationToken = default);
    Task NotifyMediaMetadataRefreshedAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task NotifyMediaPicturesUpdatedAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task NotifyPersonPicturesUpdatedAsync(Guid personId, CancellationToken cancellationToken = default);
    Task NotifyMediaIndexedFilesUpdatedAsync(Guid mediaId, Guid libraryId, CancellationToken cancellationToken = default);
    Task NotifyLibraryScanCompletedAsync(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount, CancellationToken cancellationToken = default);
    Task NotifyLibraryScanProgressAsync(Guid libraryId, int processed, int total, string phase, CancellationToken cancellationToken = default);
}
