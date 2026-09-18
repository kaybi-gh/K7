using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Broadcasts custom navigation layout updates via the K7 hub to all connected clients of a user.
/// </summary>
internal sealed class UserCustomNavNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    ILogger<UserCustomNavNotifier> logger) : IUserCustomNavNotifier
{
    public async Task NotifyCustomNavLayoutUpdatedAsync(
        string identityUserId,
        CustomNavLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending custom nav layout to group '{IdentityUserId}'",
            identityUserId);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceiveCustomNavLayoutUpdated(layout);
    }
}
