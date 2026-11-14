using K7.Shared.Theming;

namespace K7.Shared.Dtos;

public class ServerInfoDto
{
    public bool GuestEnabled { get; set; }
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultTheme { get; set; } = "default-dark";

    /// <summary>Themes made by an administrator, offered to every account next to the built-in ones.</summary>
    public List<CustomThemeDto> ServerThemes { get; set; } = [];
    public PasswordPolicyDto PasswordPolicy { get; set; } = PasswordPolicyDto.Defaults;
}
