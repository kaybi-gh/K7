using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.AudioMuseAi.Queries.GetTrackSuggestions;

[Authorize(Roles = Roles.User)]
public record GetTrackSuggestionsQuery(List<Guid> RecentTrackIds, int Count = 20) : IRequest<List<Guid>>;

public class GetTrackSuggestionsQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetTrackSuggestionsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetTrackSuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (request.RecentTrackIds.Count == 0 || request.RecentTrackIds.Count > 1)
            return await musicIntelligenceService.GetDiscoveryTracksAsync(request.Count, cancellationToken);

        return await musicIntelligenceService.GetSimilarTracksAsync(request.RecentTrackIds[0], request.Count, cancellationToken);
    }
}
