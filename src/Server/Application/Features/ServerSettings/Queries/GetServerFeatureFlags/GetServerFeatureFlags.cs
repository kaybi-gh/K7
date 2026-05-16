using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ServerSettings.Queries.GetServerFeatureFlags;

[Authorize(Roles = Roles.Administrator)]
public record GetServerFeatureFlagsQuery : IRequest<ServerFeatureFlagsDto>;

public class GetServerFeatureFlagsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetServerFeatureFlagsQuery, ServerFeatureFlagsDto>
{
    public async Task<ServerFeatureFlagsDto> Handle(GetServerFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}
