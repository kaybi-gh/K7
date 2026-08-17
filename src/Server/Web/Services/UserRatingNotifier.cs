using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Broadcasts user rating updates via the K7 hub to all connected clients of a user.
/// </summary>
internal sealed class UserRatingNotifier(IHubContext<K7Hub, IK7HubClient> hubContext, ILogger<UserRatingNotifier> logger) : IUserRatingNotifier
{
    public async Task NotifyUserRatingUpdatedAsync(
        string identityUserId,
        Guid mediaId,
        int value,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending rating to group '{IdentityUserId}' for media {MediaId} (value={Value})",
            identityUserId, mediaId, value);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceiveUserRatingUpdated(mediaId, value);
    }
}
