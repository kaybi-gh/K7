namespace K7.Shared.Theming;

/// <summary>
/// A selectable theme. Built-in themes are named by <see cref="Id"/> through localization;
/// custom themes carry the name the user gave them.
/// </summary>
public sealed record ThemeDefinition(string Id, ThemePaletteDto Palette, bool IsBuiltIn = true, string? CustomName = null)
{
    /// <summary>
    /// The two base themes are written in <c>css/themes/</c>. Their colors are only emitted
    /// again when the user overrides the accent.
    /// </summary>
    public bool IsBase => Id is BuiltInThemes.DefaultDarkId or BuiltInThemes.DefaultLightId;

    /// <summary>Value of <c>data-theme</c> on the root element: the base this theme builds on.</summary>
    public string BaseAttribute => Palette.IsDark ? BuiltInThemes.DefaultDarkId : BuiltInThemes.DefaultLightId;
}
