using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TranscodeSettings.Queries.GetTranscodeSettings;

[Authorize(Roles = Roles.Administrator)]
public record GetTranscodeSettingsQuery : IRequest<TranscodeSettingsDto>;

public class GetTranscodeSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetTranscodeSettingsQuery, TranscodeSettingsDto>
{
    public async Task<TranscodeSettingsDto> Handle(GetTranscodeSettingsQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.TranscodeSettings, cancellationToken);
        if (string.IsNullOrEmpty(json))
            return new TranscodeSettingsDto();

        return JsonSerializer.Deserialize<TranscodeSettingsDto>(json) ?? new TranscodeSettingsDto();
    }
}
