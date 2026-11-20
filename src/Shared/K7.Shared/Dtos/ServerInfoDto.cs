using K7.Shared.Theming;

namespace K7.Shared.Dtos;

public class ServerInfoDto
{
    public bool GuestEnabled { get; set; }
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultTheme { get; set; } = "default-dark";

    /// <summary>Themes made by an administrator, offered to every account next to the built-in ones.</summary>
    public List<CustomThemeDto> ServerThemes { get; set; } = [];

    /// <summary>Card style for accounts that chose none. Null means the built-in card.</summary>
    public MediaCardPreferencesDto? DefaultCards { get; set; }

    /// <summary>
    /// Media pages and players for accounts that did not set them up (only the skin members
    /// are read). Null means the built-in ones.
    /// </summary>
    public AppearanceSettingsDto? DefaultSkins { get; set; }

    /// <summary>
    /// Types of devices that have media pages and players of their own instead of
    /// <see cref="DefaultSkins"/>. Null when every device shows the same ones.
    /// </summary>
    public Dictionary<K7.Server.Domain.Enums.DeviceType, AppearanceSettingsDto>? DefaultSkinsByDevice { get; set; }

    /// <summary>The default media pages and players as one value, null for the built-in ones.</summary>
    public ServerSkinsDto? ToServerSkins() =>
        DefaultSkins is null && DefaultSkinsByDevice is not { Count: > 0 }
            ? null
            : new ServerSkinsDto
            {
                Default = DefaultSkins ?? new AppearanceSettingsDto(),
                DeviceOverrides = DefaultSkinsByDevice ?? []
            };
    public PasswordPolicyDto PasswordPolicy { get; set; } = PasswordPolicyDto.Defaults;
}
