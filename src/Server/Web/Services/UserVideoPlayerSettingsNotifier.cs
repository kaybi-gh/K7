using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

/// <summary>
/// Broadcasts video player UX settings updates via the K7 hub to all connected clients of a user.
/// </summary>
internal sealed class UserVideoPlayerSettingsNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    ILogger<UserVideoPlayerSettingsNotifier> logger) : IUserVideoPlayerSettingsNotifier
{
    public async Task NotifyVideoPlayerSettingsUpdatedAsync(
        string identityUserId,
        VideoPlayerSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Sending video player UX settings to group '{IdentityUserId}'",
            identityUserId);

        await hubContext.Clients
            .Group(identityUserId)
            .ReceiveVideoPlayerSettingsUpdated(settings);
    }
}
