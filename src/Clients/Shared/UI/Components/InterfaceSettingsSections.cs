using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>Addresses and sub-page keys of the Interface settings (personal and admin).</summary>
public static class InterfaceSettingsSections
{
    public const string SettingsBasePath = "/settings/interface";
    public const string AdminBasePath = "/admin/interface";

    public const string Appearance = "appearance";
    public const string Home = "home";
    public const string Cards = "cards";
    public const string Navigation = "navigation";
    public const string ThemeSongs = "theme-songs";

    /// <summary>True when the current address is the theme songs sub-page.</summary>
    public static bool IsThemeSongs(NavigationManager navigation) =>
        CustomNavPath.From(navigation).EndsWith("/" + ThemeSongs, StringComparison.OrdinalIgnoreCase);
}
