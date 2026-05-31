using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.DiscoverPeerLibraries;

[Authorize(Roles = Roles.Administrator)]
public record DiscoverPeerLibrariesCommand(Guid PeerId) : IRequest<IReadOnlyList<PeerShareAgreementDto>>;

public class DiscoverPeerLibrariesCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<DiscoverPeerLibrariesCommandHandler> logger)
    : IRequestHandler<DiscoverPeerLibrariesCommand, IReadOnlyList<PeerShareAgreementDto>>
{
    public async Task<IReadOnlyList<PeerShareAgreementDto>> Handle(DiscoverPeerLibrariesCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerId && p.Status == PeerStatus.Active, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        if (peer.OutboundClientId is null || peer.OutboundClientSecret is null)
        {
            logger.LogWarning("Peer {PeerName} has no outbound credentials, cannot discover libraries", peer.Name);
            return await GetInboundAgreementsAsync(peer.Id, cancellationToken);
        }

        var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
        if (token is null)
        {
            logger.LogWarning("Failed to get access token for peer {PeerName} during library discovery", peer.Name);
            return await GetInboundAgreementsAsync(peer.Id, cancellationToken);
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
                var agreement = new PeerShareAgreement
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    LibraryId = localLibrary.Id,
                    Direction = ShareDirection.Inbound,
                    IsEnabled = peer.AutoAddNewLibraries
                };
                agreement.Library = localLibrary;
                context.PeerShareAgreements.Add(agreement);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return await GetInboundAgreementsAsync(peer.Id, cancellationToken);
    }

    private async Task<IReadOnlyList<PeerShareAgreementDto>> GetInboundAgreementsAsync(Guid peerId, CancellationToken cancellationToken)
    {
        var agreements = await context.PeerShareAgreements
            .Include(a => a.Library)
            .Where(a => a.PeerServerId == peerId && a.Direction == ShareDirection.Inbound)
            .ToListAsync(cancellationToken);

        return agreements
            .DistinctBy(a => a.LibraryId)
            .Select(a => a.ToPeerShareAgreementDto())
            .ToList();
    }
}
