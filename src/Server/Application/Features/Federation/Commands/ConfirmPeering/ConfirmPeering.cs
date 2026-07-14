using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.ConfirmPeering;

public record ConfirmPeeringCommand : IRequest
{
    public required string Token { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public string? FederationAssertionSecret { get; init; }
}

public class ConfirmPeeringCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IFederationNotifier federationNotifier,
    ILogger<ConfirmPeeringCommandHandler> logger)
    : IRequestHandler<ConfirmPeeringCommand>
{
    public async Task Handle(ConfirmPeeringCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Status == PeerStatus.Pending
                && p.PeeringToken == request.Token, cancellationToken);

        if (peer is null)
            throw new InvalidOperationException("No pending peer found for the supplied token.");

        peer.PeeringToken = null;

        peer.OutboundClientId = request.ClientId;
        peer.OutboundClientSecret = request.ClientSecret;
        peer.FederationAssertionSecret = request.FederationAssertionSecret;
        peer.Status = PeerStatus.Active;
        peer.LastSeen = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await federationNotifier.NotifyPeerStateChangedAsync(peer.Id, PeerStatus.Active, cancellationToken);

        // Discover remote libraries and create inbound agreements (without syncing media)
        await DiscoverRemoteLibrariesAsync(peer, cancellationToken);
    }

    private async Task DiscoverRemoteLibrariesAsync(PeerServer peer, CancellationToken cancellationToken)
    {
        try
        {
            var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);
            if (token is null)
            {
                logger.LogWarning("Failed to get access token for peer {PeerName} during library discovery", peer.Name);
                return;
            }

            var remoteLibraries = await peerClient.GetRemoteLibrariesAsync(peer.BaseUrl, token, cancellationToken);

            foreach (var remoteLibrary in remoteLibraries)
            {
                var localLibrary = await context.Libraries
                    .FirstOrDefaultAsync(l => l.PeerServerId == peer.Id && l.Title == remoteLibrary.Title, cancellationToken);

                if (localLibrary is null)
                {
                    var group = await context.LibraryGroups
                        .FirstOrDefaultAsync(g => g.MediaType == remoteLibrary.MediaType, cancellationToken);

                    if (group is null)
                    {
                        group = new LibraryGroup
                        {
                            Id = Guid.NewGuid(),
                            Title = $"{peer.Name} - {remoteLibrary.MediaType}",
                            MediaType = remoteLibrary.MediaType
                        };
                        context.LibraryGroups.Add(group);
                    }

                    localLibrary = new Library
                    {
                        Id = Guid.NewGuid(),
                        Title = remoteLibrary.Title,
                        MediaType = remoteLibrary.MediaType,
                        MetadataProviderName = "federation",
                        MetadataLanguage = "en",
                        MetadataFallbackLanguage = "en",
                        LibraryGroupId = group.Id,
                        PeerServerId = peer.Id
                    };
                    context.Libraries.Add(localLibrary);
                    await context.SaveChangesAsync(cancellationToken);
                }

                var existingAgreement = await context.PeerShareAgreements
                    .FirstOrDefaultAsync(a => a.PeerServerId == peer.Id
                        && a.LibraryId == localLibrary.Id
                        && a.Direction == ShareDirection.Inbound, cancellationToken);

                if (existingAgreement is null)
                {
                    context.PeerShareAgreements.Add(new PeerShareAgreement
                    {
                        Id = Guid.NewGuid(),
                        PeerServerId = peer.Id,
                        LibraryId = localLibrary.Id,
                        Direction = ShareDirection.Inbound,
                        IsEnabled = true
                    });
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to discover remote libraries for peer {PeerName} (best-effort)", peer.Name);
        }
    }
}
