using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Broadcasts appearance preference updates via the K7 hub to all connected clients of a user.
/// </summary>
internal sealed class UserAppearanceNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    ILogger<UserAppearanceNotifier> logger) : IUserAppearanceNotifier
{
    public async Task NotifyAppearancePreferencesUpdatedAsync(
        string identityUserId,
        AppearancePreferencesDto preferences,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending appearance preferences to group '{IdentityUserId}'",
            identityUserId);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceiveAppearancePreferencesUpdated(preferences);
    }
}
