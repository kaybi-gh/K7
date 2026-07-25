using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Broadcasts playback progress updates via the K7 hub to all connected clients of a user.
/// </summary>
internal sealed class PlaybackProgressNotifier(IHubContext<K7Hub, IK7HubClient> hubContext, ILogger<PlaybackProgressNotifier> logger) : IPlaybackProgressNotifier
{
    public async Task NotifyProgressUpdatedAsync(
        string identityUserId,
        Guid mediaId,
        double progressPercentage,
        bool isCompleted,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending progress to group '{IdentityUserId}' for media {MediaId} ({Progress:F1}%, completed={IsCompleted}, type={MediaType})",
            identityUserId, mediaId, progressPercentage, isCompleted, mediaType);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceivePlaybackProgress(mediaId, progressPercentage, isCompleted, mediaType);
    }
}
