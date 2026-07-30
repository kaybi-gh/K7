using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetTrackSuggestions;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetTrackSuggestionsQuery(List<Guid> RecentTrackIds, int Count = 20) : IRequest<List<MusicIntelligenceTrackMatchDto>>;

public class GetTrackSuggestionsQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetTrackSuggestionsQuery, List<MusicIntelligenceTrackMatchDto>>
{
    public async Task<List<MusicIntelligenceTrackMatchDto>> Handle(GetTrackSuggestionsQuery request, CancellationToken cancellationToken)
    {
        // Suggestions = discovery (fresh finds), distinct from Similar (neighbors of current track).
        var discoveryIds = await musicIntelligenceService.GetDiscoveryTracksAsync(request.Count, cancellationToken);
        var recent = request.RecentTrackIds.ToHashSet();
        return discoveryIds
            .Where(id => !recent.Contains(id))
            .Select(id => new MusicIntelligenceTrackMatchDto { ItemId = id })
            .Take(request.Count)
            .ToList();
    }
}
