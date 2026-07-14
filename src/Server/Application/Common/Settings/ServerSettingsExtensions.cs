using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Settings;

public static class ServerSettingsExtensions
{
    public static async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(
        this IServerSettingsService serverSettingsService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await serverSettingsService.GetAsync(ApplicationSettingKeys.FeatureFlags, cancellationToken)
                ?? new ServerFeatureFlagsDto();
        }
        catch (JsonException)
        {
            var legacyJson = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
            if (string.IsNullOrWhiteSpace(legacyJson))
                return new ServerFeatureFlagsDto();

            var flags = JsonSerializer.Deserialize<ServerFeatureFlagsDto>(legacyJson)
                ?? new ServerFeatureFlagsDto();

            await serverSettingsService.SetAsync(ApplicationSettingKeys.FeatureFlags, flags, cancellationToken);
            return flags;
        }
    }
}
