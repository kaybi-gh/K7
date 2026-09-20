using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace K7.Server.Web.Services;

internal sealed class NowPlayingNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    IActiveStreamTracker tracker) : INowPlayingNotifier
{
    public Task NotifyAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        var sessions = NowPlayingMapper.FromTracker(tracker, identityUserId);
        return hubContext.Clients.Group(identityUserId).ReceiveNowPlayingUpdated(sessions);
    }
}
