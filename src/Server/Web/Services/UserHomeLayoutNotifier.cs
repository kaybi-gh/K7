using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Tells all connected clients of a user, via the K7 hub, that their home layout changed.
/// </summary>
internal sealed class UserHomeLayoutNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    ILogger<UserHomeLayoutNotifier> logger) : IUserHomeLayoutNotifier
{
    public async Task NotifyHomeLayoutUpdatedAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending home layout change to group '{IdentityUserId}'",
            identityUserId);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceiveHomeLayoutUpdated();
    }
}
