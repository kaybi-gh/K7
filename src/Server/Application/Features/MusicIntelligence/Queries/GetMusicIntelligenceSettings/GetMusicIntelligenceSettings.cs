using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Queries.GetMusicIntelligenceSettings;

[Authorize(Roles = Roles.Administrator)]
public record GetMusicIntelligenceSettingsQuery : IRequest<MusicIntelligenceSettingsDto>;

public class GetMusicIntelligenceSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetMusicIntelligenceSettingsQuery, MusicIntelligenceSettingsDto>
{
    public async Task<MusicIntelligenceSettingsDto> Handle(GetMusicIntelligenceSettingsQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.AudioMuseAi, cancellationToken);

        if (string.IsNullOrEmpty(json))
            return new MusicIntelligenceSettingsDto();

        return JsonSerializer.Deserialize<MusicIntelligenceSettingsDto>(json) ?? new MusicIntelligenceSettingsDto();
    }
}
