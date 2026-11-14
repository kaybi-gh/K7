using System.Text;
using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>What the client writes on the root element for one resolved appearance.</summary>
/// <param name="BaseAttribute">Value of <c>data-theme</c> (dark or light base).</param>
/// <param name="Declarations">CSS declarations layered over the base theme. Empty for a default look.</param>
/// <param name="ThemeColor">Browser and system chrome color (<c>meta[name=theme-color]</c>).</param>
public sealed record ThemeCss(string BaseAttribute, string Declarations, string ThemeColor);

/// <summary>
/// Turns a theme (data) and appearance settings into CSS custom properties. Only validated
/// hex colors and fixed strings are ever written, so a theme file cannot inject CSS.
/// </summary>
public static class ThemeCssBuilder
{
    public static ThemeCss Build(ThemeDefinition theme, AppearanceSettingsDto settings)
    {
        var palette = ThemePaletteValidator.HasValidColors(theme.Palette)
            ? theme.Palette
            : (theme.Palette.IsDark ? BuiltInThemes.DefaultDark : BuiltInThemes.DefaultLight).Palette;
        var isBase = theme.IsBase || !ReferenceEquals(palette, theme.Palette);
        var accentOverride = ThemeColorMath.IsHex(settings.AccentColor)
            ? ThemeColorMath.Normalize(settings.AccentColor!)
            : null;

        var css = new StringBuilder();

        if (!isBase)
            AppendSurfaces(css, palette);

        if (accentOverride is not null)
        {
            AppendAccent(
                css,
                accentOverride,
                ThemeColorMath.ReadableInkOn(accentOverride),
                ThemeColorMath.EnsureContrast(accentOverride, palette.Background, ThemePaletteValidator.TextRatio));
        }
        else if (!isBase)
        {
            AppendAccent(css, palette.Accent, palette.OnAccent, palette.AccentText);
        }

        AppendCorners(css, settings.CornerStyle);
        AppendTextSize(css, settings.TextSize);

        return new ThemeCss(
            palette.IsDark ? BuiltInThemes.DefaultDarkId : BuiltInThemes.DefaultLightId,
            css.ToString(),
            ThemeColorMath.Normalize(palette.Background));
    }

    private static void AppendSurfaces(StringBuilder css, ThemePaletteDto palette)
    {
        Declare(css, "--color-bg", palette.Background);
        Declare(css, "--color-surface", palette.Surface);
        Declare(css, "--color-surface-2", palette.SurfaceAlt);
        css.Append("--color-drawer:color-mix(in srgb,var(--color-bg) 65%,var(--color-surface));");
        css.Append("--color-bg-gray:color-mix(in srgb,var(--color-bg) 50%,var(--color-surface));");
        css.Append("--color-surface-hover:color-mix(in srgb,var(--color-text) 6%,transparent);");
        Declare(css, "--color-text", palette.Text);
        Declare(css, "--color-text-secondary", palette.TextSecondary);
        Declare(css, "--color-text-muted", palette.TextMuted);
        css.Append("--color-text-disabled:color-mix(in srgb,var(--color-text-muted) 55%,var(--color-bg));");
        css.Append(palette.IsDark
            ? "--color-overlay:color-mix(in srgb,var(--color-bg) 82%,transparent);"
            : "--color-overlay:color-mix(in srgb,var(--color-text) 45%,transparent);");
    }

    private static void AppendAccent(StringBuilder css, string accent, string onAccent, string accentText)
    {
        Declare(css, "--color-accent", accent);
        css.Append("--color-accent-hover:color-mix(in srgb,var(--color-accent) 82%,#fff);");
        Declare(css, "--color-accent-fg", onAccent);
        Declare(css, "--color-accent-text", accentText);
        css.Append("--color-primary:var(--color-accent);");
        css.Append("--color-primary-hover:var(--color-accent-hover);");
        Declare(css, "--color-text-on-accent", onAccent);
        css.Append("--color-text-on-media-accent:color-mix(in srgb,var(--color-accent) 70%,#fff);");
        css.Append("--color-focus:var(--color-accent);");
        css.Append("--color-border:color-mix(in srgb,var(--color-accent-text) 26%,transparent);");
        css.Append("--color-border-subtle:color-mix(in srgb,var(--color-accent-text) 13%,transparent);");
        css.Append("--scrollbar-thumb:color-mix(in srgb,var(--color-accent-text) 35%,transparent);");
    }

    private static void AppendCorners(StringBuilder css, AppearanceCornerStyle style)
    {
        switch (style)
        {
            case AppearanceCornerStyle.Square:
                css.Append("--radius-sm:0px;--radius-md:2px;--radius-lg:2px;--radius-xl:4px;--radius-2xl:4px;");
                break;
            case AppearanceCornerStyle.Round:
                css.Append("--radius-sm:6px;--radius-md:12px;--radius-lg:16px;--radius-xl:22px;--radius-2xl:30px;");
                break;
        }
    }

    // A factor, not a font size: each platform keeps its own base size (larger on TV) and
    // multiplies it, see the root font size rules in app.css.
    private static void AppendTextSize(StringBuilder css, AppearanceTextSize size)
    {
        switch (size)
        {
            case AppearanceTextSize.Small:
                css.Append("--k7-text-scale:0.875;");
                break;
            case AppearanceTextSize.Large:
                css.Append("--k7-text-scale:1.125;");
                break;
            case AppearanceTextSize.ExtraLarge:
                css.Append("--k7-text-scale:1.25;");
                break;
        }
    }

    private static void Declare(StringBuilder css, string name, string hex)
    {
        if (!ThemeColorMath.IsHex(hex))
            return;

        css.Append(name).Append(':').Append(ThemeColorMath.Normalize(hex)).Append(';');
    }
}
