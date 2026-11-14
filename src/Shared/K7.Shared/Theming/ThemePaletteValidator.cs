namespace K7.Shared.Theming;

public enum ThemePaletteIssue
{
    InvalidColor,
    TextContrast,
    SecondaryTextContrast,
    MutedTextContrast,
    OnAccentContrast,
    AccentTextContrast
}

/// <summary>
/// Checks that a palette is well formed and readable (WCAG AA for text, 3:1 for muted text).
/// Built-in themes, the theme editor and imported files all go through it.
/// </summary>
public static class ThemePaletteValidator
{
    public const double TextRatio = 4.5;
    public const double MutedTextRatio = 3;

    public static IReadOnlyList<ThemePaletteIssue> Validate(ThemePaletteDto palette)
    {
        if (!HasValidColors(palette))
            return [ThemePaletteIssue.InvalidColor];

        var issues = new List<ThemePaletteIssue>();

        if (ThemeColorMath.ContrastRatio(palette.Text, palette.Background) < TextRatio
            || ThemeColorMath.ContrastRatio(palette.Text, palette.Surface) < TextRatio
            || ThemeColorMath.ContrastRatio(palette.Text, palette.SurfaceAlt) < TextRatio)
        {
            issues.Add(ThemePaletteIssue.TextContrast);
        }

        if (ThemeColorMath.ContrastRatio(palette.TextSecondary, palette.Background) < TextRatio)
            issues.Add(ThemePaletteIssue.SecondaryTextContrast);

        if (ThemeColorMath.ContrastRatio(palette.TextMuted, palette.Background) < MutedTextRatio)
            issues.Add(ThemePaletteIssue.MutedTextContrast);

        if (ThemeColorMath.ContrastRatio(palette.OnAccent, palette.Accent) < TextRatio)
            issues.Add(ThemePaletteIssue.OnAccentContrast);

        if (ThemeColorMath.ContrastRatio(palette.AccentText, palette.Background) < TextRatio)
            issues.Add(ThemePaletteIssue.AccentTextContrast);

        return issues;
    }

    public static bool HasValidColors(ThemePaletteDto? palette) =>
        palette is not null && palette.Colors().All(ThemeColorMath.IsHex);

    public static bool IsValidCustomThemeId(string? id) => IsValidThemeId(id, CustomThemeDto.IdPrefix);

    /// <summary>A stored theme: an id with the expected prefix, a name, and well-formed colors.</summary>
    public static bool IsValidTheme(CustomThemeDto? theme, string idPrefix) =>
        theme is not null
        && IsValidThemeId(theme.Id, idPrefix)
        && !string.IsNullOrWhiteSpace(theme.Name)
        && theme.Name.Length <= CustomThemeDto.MaxNameLength
        && HasValidColors(theme.Palette);

    public static bool IsValidThemeId(string? id, string prefix)
    {
        if (string.IsNullOrEmpty(id)
            || !id.StartsWith(prefix, StringComparison.Ordinal)
            || id.Length <= prefix.Length
            || id.Length > 64)
        {
            return false;
        }

        foreach (var c in id)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                return false;
        }

        return true;
    }
}
