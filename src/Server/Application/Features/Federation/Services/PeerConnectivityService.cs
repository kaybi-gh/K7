using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface IPeerConnectivityService
{
    Task RecordConnectivityAsync(Guid peerId, bool succeeded, CancellationToken cancellationToken = default);
}

public class PeerConnectivityService(
    IApplicationDbContext context,
    IFederationNotifier federationNotifier) : IPeerConnectivityService
{
    public async Task RecordConnectivityAsync(Guid peerId, bool succeeded, CancellationToken cancellationToken = default)
    {
        var peer = await context.PeerServers.FirstOrDefaultAsync(p => p.Id == peerId, cancellationToken);
        if (peer is null)
            return;

        peer.LastTestSucceeded = succeeded;
        if (succeeded)
            peer.LastSeen = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await federationNotifier.NotifyPeerTestResultAsync(peer.Id, succeeded, cancellationToken);
    }
}
