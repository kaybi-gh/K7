using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetSonicPath;

[Authorize(Roles = Roles.User)]
public record GetSonicPathQuery(Guid FromId, Guid ToId) : IRequest<List<Guid>>;

public class GetSonicPathQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetSonicPathQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetSonicPathQuery request, CancellationToken cancellationToken)
    {
        return await musicIntelligenceService.GetSonicPathAsync(request.FromId, request.ToId, cancellationToken);
    }
}
