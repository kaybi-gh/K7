using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.EventHandlers;

/// <summary>
/// When a local library is created, create outbound share agreements for every active peer
/// with <see cref="PeerServer.AutoAddNewLibraries"/> (accept-dialog "auto share" / settings flag),
/// then best-effort notify peers that have outbound credentials.
/// </summary>
public class FederationLibraryAutoShareOnCreatedHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<FederationLibraryAutoShareOnCreatedHandler> logger)
    : INotificationHandler<LibraryCreatedEvent>
{
    public async Task Handle(LibraryCreatedEvent notification, CancellationToken cancellationToken)
    {
        var library = notification.Library;
        if (library.PeerServerId is not null)
            return;

        var peers = await context.PeerServers
            .Where(p => p.Status == PeerStatus.Active && p.AutoAddNewLibraries)
            .ToListAsync(cancellationToken);

        if (peers.Count == 0)
            return;

        var sharedWith = new List<PeerServer>();

        foreach (var peer in peers)
        {
            var alreadyShared = await context.PeerShareAgreements
                .AnyAsync(
                    a => a.PeerServerId == peer.Id
                        && a.LibraryId == library.Id
                        && a.Direction == ShareDirection.Outbound,
                    cancellationToken);

            if (alreadyShared)
                continue;

            var maxConcurrentStreams = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound)
                .Select(a => a.MaxConcurrentStreams)
                .FirstOrDefaultAsync(cancellationToken);

            context.PeerShareAgreements.Add(new PeerShareAgreement
            {
                Id = Guid.NewGuid(),
                PeerServerId = peer.Id,
                LibraryId = library.Id,
                Direction = ShareDirection.Outbound,
                MaxConcurrentStreams = maxConcurrentStreams,
                IsEnabled = true
            });
            sharedWith.Add(peer);
        }

        if (sharedWith.Count == 0)
            return;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Auto-shared new library {LibraryId} ({LibraryTitle}) with {PeerCount} peer(s)",
            library.Id,
            library.Title,
            sharedWith.Count);

        foreach (var peer in sharedWith)
            await NotifyShareUpdateBestEffortAsync(peer, cancellationToken);
    }

    private async Task NotifyShareUpdateBestEffortAsync(PeerServer peer, CancellationToken cancellationToken)
    {
        if (peer.OutboundClientId is null || peer.OutboundClientSecret is null)
        {
            logger.LogDebug(
                "Peer {PeerName} has no outbound credentials; share is live for pull/sync but push notify was skipped",
                peer.Name);
            return;
        }

        try
        {
            var token = await peerClient.GetAccessTokenAsync(
                peer.BaseUrl,
                peer.OutboundClientId,
                peer.OutboundClientSecret,
                cancellationToken);

            if (token is null)
            {
                logger.LogWarning(
                    "Failed to authenticate with peer {PeerName} for auto-share notify",
                    peer.Name);
                return;
            }

            var sharedLibraryIds = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled)
                .Select(a => a.LibraryId)
                .ToListAsync(cancellationToken);

            await peerClient.NotifyShareUpdateAsync(peer.BaseUrl, token, sharedLibraryIds, cancellationToken);
            logger.LogDebug("Notified peer {PeerName} of auto-share update", peer.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify peer {PeerName} of auto-share update (best-effort)", peer.Name);
        }
    }
}
