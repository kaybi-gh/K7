using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationLibraries;

public record GetFederationLibrariesQuery(string? ClientId) : IRequest<IReadOnlyList<PeerLibraryDto>>;

public class GetFederationLibrariesQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationLibrariesQuery, IReadOnlyList<PeerLibraryDto>>
{
    public async Task<IReadOnlyList<PeerLibraryDto>> Handle(
        GetFederationLibrariesQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);
        var sharedLibraryIds = await peerAuthorization.GetOutboundSharedLibraryIdsAsync(peer.Id, cancellationToken);

        return await context.Libraries
            .Where(l => sharedLibraryIds.Contains(l.Id) && l.PeerServerId == null)
            .Select(l => new PeerLibraryDto
            {
                Id = l.Id,
                Title = l.Title,
                MediaType = l.MediaType,
                MediaCount = l.IndexedFiles.Select(f => f.MediaId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
    }
}
