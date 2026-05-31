using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class FederationNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    ILogger<FederationNotifier> logger) : IFederationNotifier
{
    public async Task NotifyPeerStateChangedAsync(Guid peerId, PeerStatus newStatus, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting PeerStateChanged: {PeerId} -> {Status}", peerId, newStatus);
        await hubContext.Clients.Group(K7Hub.AdminFederationGroup).ReceivePeerStateChanged(peerId, newStatus);
    }

    public async Task NotifyPeerRequestReceivedAsync(PeerRequestDto request, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting PeerRequestReceived: {RequesterName}", request.RequesterName);
        await hubContext.Clients.Group(K7Hub.AdminFederationGroup).ReceivePeerRequestReceived(request);
    }

    public async Task NotifyPeerTestResultAsync(Guid peerId, bool reachable, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Broadcasting PeerTestResult: {PeerId} reachable={Reachable}", peerId, reachable);
        await hubContext.Clients.Group(K7Hub.AdminFederationGroup).ReceivePeerTestResult(peerId, reachable);
    }
}
