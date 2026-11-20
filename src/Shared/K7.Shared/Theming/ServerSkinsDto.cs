using K7.Server.Domain.Enums;

namespace K7.Shared.Theming;

/// <summary>
/// The media pages and players an administrator set for the accounts that did not set theirs up:
/// one set shared by every device, which a type of device can replace with its own, like the
/// preferences of a user (<see cref="AppearancePreferencesDto"/>). Only the skin members of the
/// sets are read.
/// </summary>
public sealed record ServerSkinsDto
{
    public AppearanceSettingsDto Default { get; set; } = new();
    public Dictionary<DeviceType, AppearanceSettingsDto> DeviceOverrides { get; set; } = [];

    /// <summary>The set a type of device shows: its own, or the shared one.</summary>
    public AppearanceSettingsDto Resolve(DeviceType deviceType) =>
        DeviceOverrides.TryGetValue(deviceType, out var settings) ? settings : Default;
}
