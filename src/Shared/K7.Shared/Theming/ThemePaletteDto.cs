namespace K7.Shared.Theming;

/// <summary>
/// A theme as data: nine colors (hex, <c>#rrggbb</c>) on a dark or light base. Every other
/// color token is derived from these, so a theme can never carry CSS.
/// </summary>
public sealed record ThemePaletteDto
{
    public bool IsDark { get; set; } = true;
    public string Background { get; set; } = "";
    public string Surface { get; set; } = "";
    public string SurfaceAlt { get; set; } = "";
    public string Text { get; set; } = "";
    public string TextSecondary { get; set; } = "";
    public string TextMuted { get; set; } = "";
    public string Accent { get; set; } = "";
    /// <summary>Ink used on top of <see cref="Accent"/> fills.</summary>
    public string OnAccent { get; set; } = "";
    /// <summary>Accent used for text and icons on <see cref="Background"/>.</summary>
    public string AccentText { get; set; } = "";

    public IEnumerable<string> Colors()
    {
        yield return Background;
        yield return Surface;
        yield return SurfaceAlt;
        yield return Text;
        yield return TextSecondary;
        yield return TextMuted;
        yield return Accent;
        yield return OnAccent;
        yield return AccentText;
    }
}
