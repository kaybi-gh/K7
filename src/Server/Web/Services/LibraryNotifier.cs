using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class LibraryNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    MediaNotificationBatcher batcher,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<LibraryNotifier> logger) : ILibraryNotifier
{
    public Task NotifyMediaAddedAsync(Guid mediaId, string? title, string mediaType, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Enqueuing MediaAdded: {MediaId} '{Title}' ({MediaType})", mediaId, title, mediaType);
        batcher.Enqueue(new MediaBatchItem
        {
            MediaId = mediaId,
            Title = title,
            MediaType = mediaType
        });
        return Task.CompletedTask;
    }

    public async Task NotifyMediaBatchAddedAsync(List<MediaBatchItem> items, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting MediaBatchAdded: {Count} items", items.Count);
        await hubContext.Clients.All.ReceiveMediaBatchAdded(items);
    }

    public async Task NotifyMediaMetadataRefreshedAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting MediaMetadataRefreshed: {MediaId}", mediaId);
        await hubContext.Clients.All.ReceiveMediaMetadataRefreshed(mediaId);
    }

    public async Task NotifyMediaPicturesUpdatedAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        cacheInvalidator.InvalidateAll();
        logger.LogDebug("Broadcasting MediaPicturesUpdated: {MediaId}", mediaId);
        await hubContext.Clients.All.ReceiveMediaPicturesUpdated(mediaId);
    }

    public async Task NotifyPersonPicturesUpdatedAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        cacheInvalidator.InvalidateAll();
        logger.LogDebug("Broadcasting PersonPicturesUpdated: {PersonId}", personId);
        await hubContext.Clients.All.ReceivePersonPicturesUpdated(personId);
    }

    public async Task NotifyMediaIndexedFilesUpdatedAsync(Guid mediaId, Guid libraryId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting MediaIndexedFilesUpdated: {MediaId} in library {LibraryId}", mediaId, libraryId);
        await hubContext.Clients.All.ReceiveMediaIndexedFilesUpdated(mediaId, libraryId);
    }

    public async Task NotifyLibraryScanCompletedAsync(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting LibraryScanCompleted: library {LibraryId}, {AddedCount} added, {SkippedCount} skipped, {InaccessiblePathCount} inaccessible", libraryId, addedCount, skippedCount, inaccessiblePathCount);
        await hubContext.Clients.All.ReceiveLibraryScanCompleted(libraryId, addedCount, skippedCount, inaccessiblePathCount);
    }

    public async Task NotifyLibraryScanProgressAsync(Guid libraryId, int processed, int total, string phase, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting LibraryScanProgress: library {LibraryId}, {Processed}/{Total}, phase {Phase}", libraryId, processed, total, phase);
        await hubContext.Clients.All.ReceiveLibraryScanProgress(libraryId, processed, total, phase);
    }
}
