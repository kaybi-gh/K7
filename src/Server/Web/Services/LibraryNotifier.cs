using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class LibraryNotifier(IHubContext<K7Hub, IK7HubClient> hubContext, ILogger<LibraryNotifier> logger) : ILibraryNotifier
{
    public async Task NotifyMediaAddedAsync(Guid mediaId, string? title, string mediaType, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting MediaAdded: {MediaId} '{Title}' ({MediaType})", mediaId, title, mediaType);
        await hubContext.Clients.All.ReceiveMediaAdded(mediaId, title, mediaType);
    }

    public async Task NotifyLibraryScanCompletedAsync(Guid libraryId, int addedCount, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting LibraryScanCompleted: library {LibraryId}, {AddedCount} added", libraryId, addedCount);
        await hubContext.Clients.All.ReceiveLibraryScanCompleted(libraryId, addedCount);
    }
}
