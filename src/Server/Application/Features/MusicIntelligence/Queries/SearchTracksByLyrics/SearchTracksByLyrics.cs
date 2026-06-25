using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.SearchTracksByLyrics;

[Authorize(Roles = Roles.User)]
public record SearchTracksByLyricsQuery : IRequest<List<Guid>>
{
    public required string Query { get; init; }
    public int Count { get; init; } = 50;
}

public class SearchTracksByLyricsQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<SearchTracksByLyricsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(SearchTracksByLyricsQuery request, CancellationToken cancellationToken)
    {
        return await musicIntelligenceService.SearchTracksByLyricsAsync(
            request.Query,
            request.Count > 0 ? request.Count : 50,
            cancellationToken);
    }
}
