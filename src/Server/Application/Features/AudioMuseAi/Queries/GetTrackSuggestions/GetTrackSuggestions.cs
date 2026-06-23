using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.AudioMuseAi.Queries.GetTrackSuggestions;

[Authorize(Roles = Roles.User)]
public record GetTrackSuggestionsQuery(List<Guid> RecentTrackIds, int Count = 20) : IRequest<List<Guid>>;

public class GetTrackSuggestionsQueryHandler(IAudioMuseAiService audioMuseAiService)
    : IRequestHandler<GetTrackSuggestionsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetTrackSuggestionsQuery request, CancellationToken cancellationToken)
    {
        return await audioMuseAiService.GetSuggestionsAsync(request.RecentTrackIds, request.Count, cancellationToken);
    }
}
