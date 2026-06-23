using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetMusicIntelligenceStatus;

[Authorize]
public record GetMusicIntelligenceStatusQuery : IRequest<MusicIntelligenceStatusDto>;

public class GetMusicIntelligenceStatusQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetMusicIntelligenceStatusQuery, MusicIntelligenceStatusDto>
{
    public async Task<MusicIntelligenceStatusDto> Handle(GetMusicIntelligenceStatusQuery request, CancellationToken cancellationToken)
    {
        var isAvailable = await musicIntelligenceService.IsAvailableAsync(cancellationToken);
        return new MusicIntelligenceStatusDto
        {
            IsEnabled = isAvailable,
            IsAvailable = isAvailable,
            Provider = "audiomuse"
        };
    }
}
