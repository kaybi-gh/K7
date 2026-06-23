using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.AudioMuseAi.Queries.GetSimilarTracks;

[Authorize(Roles = Roles.User)]
public record GetSimilarTracksQuery(Guid TrackId, int Count = 20) : IRequest<List<Guid>>;

public class GetSimilarTracksQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetSimilarTracksQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetSimilarTracksQuery request, CancellationToken cancellationToken)
    {
        return await musicIntelligenceService.GetSimilarTracksAsync(request.TrackId, request.Count, cancellationToken);
    }
}
