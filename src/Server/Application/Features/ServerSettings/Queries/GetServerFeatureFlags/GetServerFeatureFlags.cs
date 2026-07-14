using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ServerSettings.Queries.GetServerFeatureFlags;

[Authorize]
public record GetServerFeatureFlagsQuery : IRequest<ServerFeatureFlagsDto>;

public class GetServerFeatureFlagsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetServerFeatureFlagsQuery, ServerFeatureFlagsDto>
{
    public Task<ServerFeatureFlagsDto> Handle(GetServerFeatureFlagsQuery request, CancellationToken cancellationToken) =>
        serverSettingsService.GetFeatureFlagsAsync(cancellationToken);
}
