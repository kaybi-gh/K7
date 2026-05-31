using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Federation.Queries.GetPeerRequests;

[Authorize(Roles = Roles.Administrator)]
public record GetPeerRequestsQuery : IRequest<IReadOnlyList<PeerRequestDto>>;

public class GetPeerRequestsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPeerRequestsQuery, IReadOnlyList<PeerRequestDto>>
{
    public async Task<IReadOnlyList<PeerRequestDto>> Handle(GetPeerRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await context.PeerRequests
            .Where(r => r.Status == Domain.Enums.PeerRequestStatus.Pending)
            .OrderByDescending(r => r.Created)
            .ToListAsync(cancellationToken);

        return requests.Select(r => r.ToPeerRequestDto()).ToList();
    }
}
