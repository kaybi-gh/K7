using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class BackgroundTaskNotifier(IHubContext<K7Hub, IK7HubClient> hubContext, ILogger<BackgroundTaskNotifier> logger) : IBackgroundTaskNotifier
{
    public async Task NotifyBackgroundTaskUpdatedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting BackgroundTaskUpdated");
        await hubContext.Clients.All.ReceiveBackgroundTaskUpdated();
    }
}
