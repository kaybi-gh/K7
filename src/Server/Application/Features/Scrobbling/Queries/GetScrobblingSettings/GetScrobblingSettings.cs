using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Queries.GetScrobblingSettings;

[Authorize(Roles = Roles.Administrator)]
public record GetScrobblingSettingsQuery : IRequest<ScrobblingSettingsDto>;

public class GetScrobblingSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetScrobblingSettingsQuery, ScrobblingSettingsDto>
{
    public async Task<ScrobblingSettingsDto> Handle(
        GetScrobblingSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.Scrobbling, cancellationToken);
        if (string.IsNullOrEmpty(json))
            return new ScrobblingSettingsDto();

        return JsonSerializer.Deserialize<ScrobblingSettingsDto>(json) ?? new ScrobblingSettingsDto();
    }
}
