using K7.Server.Domain.Enums;

namespace K7.Shared.Theming;

/// <summary>
/// Appearance preferences of one user, shared by all their devices. A device type can replace
/// <see cref="Default"/> with its own set (a TV with larger text, a phone with another theme).
/// </summary>
public sealed record AppearancePreferencesDto
{
    public const int MaxCustomThemes = 20;

    public AppearanceSettingsDto Default { get; set; } = new();
    public Dictionary<DeviceType, AppearanceSettingsDto> DeviceOverrides { get; set; } = [];
    public List<CustomThemeDto> CustomThemes { get; set; } = [];

    public bool HasDeviceOverride(DeviceType deviceType) => DeviceOverrides.ContainsKey(deviceType);

    public AppearanceSettingsDto Resolve(DeviceType deviceType) =>
        DeviceOverrides.TryGetValue(deviceType, out var settings) ? settings : Default;

    public CustomThemeDto? FindCustomTheme(string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : CustomThemes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
}
