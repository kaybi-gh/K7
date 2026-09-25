namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// MAUI chrome colors aligned with <c>wwwroot/css/themes/default-*.css</c>
/// (<c>--color-accent</c>, <c>--color-on-primary</c>, on-media frosted actions).
/// Hex belongs in theme sheets for Blazor; native overlays cannot read CSS variables.
/// </summary>
internal static class NativeOverlayTheme
{
    /// <summary><c>--color-accent</c> / <c>--color-primary</c>.</summary>
    public static readonly Color Accent = Color.FromArgb("#d9b060");

    /// <summary><c>--color-on-primary</c> / <c>--color-accent-fg</c>.</summary>
    public static readonly Color OnAccent = Color.FromArgb("#241a0c");

    /// <summary>
    /// Secondary action on artwork. Darker than Blazor's frosted white so Replay stays
    /// readable without backdrop-filter.
    /// </summary>
    public static readonly Color OnMediaAction = Color.FromArgb("#CC1A1410");

    public static readonly Color OnMediaActionBorder = Color.FromArgb("#59FFFFFF");

    public static readonly Color OnMediaActionFocus = Color.FromArgb("#E61A1410");
}
