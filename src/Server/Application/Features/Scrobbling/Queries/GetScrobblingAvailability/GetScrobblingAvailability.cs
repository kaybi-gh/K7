using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Queries.GetScrobblingAvailability;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetScrobblingAvailabilityQuery : IRequest<ScrobblingAvailabilityDto>;

public class GetScrobblingAvailabilityQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetScrobblingAvailabilityQuery, ScrobblingAvailabilityDto>
{
    public async Task<ScrobblingAvailabilityDto> Handle(
        GetScrobblingAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.Scrobbling, cancellationToken);
        var settings = string.IsNullOrEmpty(json)
            ? new ScrobblingSettingsDto()
            : JsonSerializer.Deserialize<ScrobblingSettingsDto>(json) ?? new ScrobblingSettingsDto();

        return new ScrobblingAvailabilityDto
        {
            Enabled = settings.Enabled,
            LastFmConfigured = !string.IsNullOrWhiteSpace(settings.LastFmApiKey)
                && !string.IsNullOrWhiteSpace(settings.LastFmApiSecret),
            TraktConfigured = !string.IsNullOrWhiteSpace(settings.TraktClientId)
                && !string.IsNullOrWhiteSpace(settings.TraktClientSecret)
        };
    }
}
