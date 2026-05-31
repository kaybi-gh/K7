using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.EventHandlers;

public class FederationMediaNotifier(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<FederationMediaNotifier> logger)
    : INotificationHandler<MediaAddedEvent>,
      INotificationHandler<MediaMetadataRefreshedEvent>
{
    public async Task Handle(MediaAddedEvent notification, CancellationToken cancellationToken)
    {
        await NotifyPeersAsync(notification.Media, PeerMediaNotificationType.Added, cancellationToken);
    }

    public async Task Handle(MediaMetadataRefreshedEvent notification, CancellationToken cancellationToken)
    {
        await NotifyPeersAsync(notification.Media, PeerMediaNotificationType.Modified, cancellationToken);
    }

    private async Task NotifyPeersAsync(Domain.Entities.Medias.BaseMedia media, PeerMediaNotificationType type, CancellationToken cancellationToken)
    {
        // Only notify for local media (not federated ones)
        if (media.PeerServerId is not null)
            return;

        // Find libraries that contain this media's files
        var libraryIds = await context.IndexedFiles
            .Where(f => f.MediaId == media.Id)
            .Select(f => f.LibraryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (libraryIds.Count == 0)
            return;

        // Find peers with outbound share agreements on those libraries
        var peersToNotify = await context.PeerShareAgreements
            .Where(a => a.Direction == ShareDirection.Outbound
                && a.IsEnabled
                && libraryIds.Contains(a.LibraryId))
            .Include(a => a.PeerServer)
            .Where(a => a.PeerServer!.Status == PeerStatus.Active
                && a.PeerServer.OutboundClientId != null
                && a.PeerServer.OutboundClientSecret != null)
            .Select(a => a.PeerServer!)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var peer in peersToNotify)
        {
            try
            {
                var token = await peerClient.GetAccessTokenAsync(
                    peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);

                if (token is null)
                {
                    logger.LogWarning("Failed to authenticate with peer {PeerName} for media notification", peer.Name);
                    continue;
                }

                var libraryId = libraryIds.First();
                await peerClient.NotifyMediaAsync(peer.BaseUrl, token, libraryId, media.Id, type, cancellationToken);
                logger.LogDebug("Notified peer {PeerName} of {Type} for media {MediaId}", peer.Name, type, media.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify peer {PeerName} of media {Type} for {MediaId}", peer.Name, type, media.Id);
            }
        }
    }
}
