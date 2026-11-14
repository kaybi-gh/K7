using K7.Clients.Shared.UI.Components;
using K7.Shared.Theming;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class ThemeNames
{
    /// <summary>Localized name of a built-in theme, or the name the user gave a custom one.</summary>
    public static string Get(IStringLocalizer<K7ThemePicker> localizer, ThemeDefinition theme)
    {
        if (!theme.IsBuiltIn)
            return string.IsNullOrWhiteSpace(theme.CustomName) ? theme.Id : theme.CustomName;

        var name = localizer[$"ThemeName_{theme.Id}"];
        return name.ResourceNotFound ? theme.Id : name.Value;
    }
}
