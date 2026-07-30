using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetMusicIntelligenceStatus;

[Authorize]
public record GetMusicIntelligenceStatusQuery : IRequest<MusicIntelligenceStatusDto>;

public class GetMusicIntelligenceStatusQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetMusicIntelligenceStatusQuery, MusicIntelligenceStatusDto>
{
    public Task<MusicIntelligenceStatusDto> Handle(
        GetMusicIntelligenceStatusQuery request,
        CancellationToken cancellationToken) =>
        musicIntelligenceService.GetStatusAsync(cancellationToken);
}
