using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface IPeerConnectivityService
{
    Task RecordConnectivityAsync(Guid peerId, bool succeeded, CancellationToken cancellationToken = default);
}

public class PeerConnectivityService(
    IApplicationDbContext context,
    IFederationNotifier federationNotifier,
    IMediaQueryCacheInvalidator cacheInvalidator) : IPeerConnectivityService
{
    public async Task RecordConnectivityAsync(Guid peerId, bool succeeded, CancellationToken cancellationToken = default)
    {
        var peer = await context.PeerServers.FirstOrDefaultAsync(p => p.Id == peerId, cancellationToken);
        if (peer is null)
            return;

        var wasVisible = IsPeerContentVisible(peer.Status, peer.LastTestSucceeded);
        var previousSucceeded = peer.LastTestSucceeded;

        peer.LastTestSucceeded = succeeded;
        if (succeeded)
            peer.LastSeen = DateTimeOffset.UtcNow;

        var isVisible = IsPeerContentVisible(peer.Status, peer.LastTestSucceeded);
        var stateTransitioned = previousSucceeded != succeeded;
        var visibilityTransitioned = wasVisible != isVisible;

        if (stateTransitioned || visibilityTransitioned)
        {
            peer.AddDomainEvent(new PeerConnectivityChangedEvent(peer, succeeded, previousSucceeded));
        }

        await context.SaveChangesAsync(cancellationToken);

        if (visibilityTransitioned)
            cacheInvalidator.InvalidateAll();

        await federationNotifier.NotifyPeerTestResultAsync(peer.Id, succeeded, cancellationToken);
    }

    private static bool IsPeerContentVisible(PeerStatus status, bool? lastTestSucceeded) =>
        status == PeerStatus.Active && lastTestSucceeded != false;
}
