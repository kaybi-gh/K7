using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Settings;

public static class ServerSettingsExtensions
{
    public static async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(
        this IServerSettingsService serverSettingsService,
        CancellationToken cancellationToken = default)
    {
        return await serverSettingsService.GetAsync(ApplicationSettingKeys.FeatureFlags, cancellationToken)
            ?? new ServerFeatureFlagsDto();
    }
}
