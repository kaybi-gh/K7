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
        logger.LogDebug("Broadcasting MediaPicturesUpdated: {MediaId}", mediaId);
        await hubContext.Clients.All.ReceiveMediaPicturesUpdated(mediaId);
    }

    public async Task NotifyLibraryScanCompletedAsync(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting LibraryScanCompleted: library {LibraryId}, {AddedCount} added, {SkippedCount} skipped, {InaccessiblePathCount} inaccessible", libraryId, addedCount, skippedCount, inaccessiblePathCount);
        await hubContext.Clients.All.ReceiveLibraryScanCompleted(libraryId, addedCount, skippedCount, inaccessiblePathCount);
    }
}
