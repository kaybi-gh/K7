using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.SearchTracksBySonicText;

[Authorize(Roles = Roles.User)]
public record SearchTracksBySonicTextQuery : IRequest<List<Guid>>
{
    public required string Query { get; init; }
    public int Count { get; init; } = 50;
}

public class SearchTracksBySonicTextQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<SearchTracksBySonicTextQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(SearchTracksBySonicTextQuery request, CancellationToken cancellationToken)
    {
        return await musicIntelligenceService.SearchTracksBySonicTextAsync(
            request.Query,
            request.Count > 0 ? request.Count : 50,
            cancellationToken);
    }
}
