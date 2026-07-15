using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Settings;

public static class ApplicationSettingKeys
{
    public static readonly SettingKey<ServerFeatureFlagsDto> FeatureFlags = new("FeatureFlags", new ServerFeatureFlagsDto());
}
