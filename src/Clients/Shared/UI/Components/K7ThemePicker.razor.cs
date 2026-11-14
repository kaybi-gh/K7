using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Single choice among themes, each shown as a small preview drawn with its own colors.
/// The preview colors are theme data, passed as custom properties on the swatch.
/// </summary>
public partial class K7ThemePicker
{
    [Parameter, EditorRequired] public IReadOnlyList<ThemeDefinition> Themes { get; set; } = [];
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Class { get; set; }

    private async Task OnThemeClicked(string themeId)
    {
        if (string.Equals(themeId, Value, StringComparison.Ordinal))
            return;

        await ValueChanged.InvokeAsync(themeId);
    }

    private static string? SwatchStyle(ThemeDefinition theme)
    {
        var palette = theme.Palette;
        if (!ThemePaletteValidator.HasValidColors(palette))
            return null;

        return $"--swatch-bg:{palette.Background};--swatch-surface:{palette.Surface};"
               + $"--swatch-text:{palette.Text};--swatch-muted:{palette.TextMuted};"
               + $"--swatch-accent:{palette.Accent};";
    }
}
