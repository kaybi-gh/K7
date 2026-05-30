using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetPeers;

[Authorize(Roles = Roles.Administrator)]
public record GetPeersQuery : IRequest<IReadOnlyList<PeerServerDto>>;

public class GetPeersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPeersQuery, IReadOnlyList<PeerServerDto>>
{
    public async Task<IReadOnlyList<PeerServerDto>> Handle(GetPeersQuery request, CancellationToken cancellationToken)
    {
        var peers = await context.PeerServers
            .Include(p => p.ShareAgreements)
                .ThenInclude(a => a.Library)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return peers.Select(p => p.ToPeerServerDto()).ToList();
    }
}
