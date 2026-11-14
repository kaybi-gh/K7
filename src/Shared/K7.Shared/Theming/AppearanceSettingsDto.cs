using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>One set of appearance choices. Every member left at its default follows the server.</summary>
public sealed record AppearanceSettingsDto
{
    /// <summary>Built-in or custom theme id. Null follows the server default theme.</summary>
    public string? ThemeId { get; set; }

    /// <summary>Accent override (<c>#rrggbb</c>). Null keeps the accent of the theme.</summary>
    public string? AccentColor { get; set; }

    public AppearanceTextSize TextSize { get; set; }
    public AppearanceCornerStyle CornerStyle { get; set; }

    public bool IsDefault =>
        ThemeId is null
        && AccentColor is null
        && TextSize == AppearanceTextSize.Default
        && CornerStyle == AppearanceCornerStyle.Default;
}
