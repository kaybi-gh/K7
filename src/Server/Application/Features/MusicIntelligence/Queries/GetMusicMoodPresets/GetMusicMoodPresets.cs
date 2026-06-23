using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using MediatR;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetMusicMoodPresets;

public record GetMusicMoodPresetsQuery : IRequest<IReadOnlyList<MusicMoodPresetDto>>;

public class GetMusicMoodPresetsQueryHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetMusicMoodPresetsQuery, IReadOnlyList<MusicMoodPresetDto>>
{
    public Task<IReadOnlyList<MusicMoodPresetDto>> Handle(GetMusicMoodPresetsQuery request, CancellationToken cancellationToken)
        => musicIntelligenceService.GetMoodPresetsAsync(cancellationToken);
}
