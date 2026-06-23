using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioMuseAi.Queries.GetAudioMuseAiSettings;

[Authorize(Roles = Roles.Administrator)]
public record GetAudioMuseAiSettingsQuery : IRequest<AudioMuseAiSettingsDto>;

public class GetAudioMuseAiSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetAudioMuseAiSettingsQuery, AudioMuseAiSettingsDto>
{
    public async Task<AudioMuseAiSettingsDto> Handle(GetAudioMuseAiSettingsQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.AudioMuseAi, cancellationToken);

        if (string.IsNullOrEmpty(json))
            return new AudioMuseAiSettingsDto();

        return JsonSerializer.Deserialize<AudioMuseAiSettingsDto>(json) ?? new AudioMuseAiSettingsDto();
    }
}
