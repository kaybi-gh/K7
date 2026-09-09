namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Couch / 10-foot device detection and viewport scale.
/// Keep tokens and the 1920 CSS-px target in sync with <c>tv-layout.js</c>.
/// </summary>
public static class TelevisionLayout
{
    public const string UserAgentMarker = "K7TV/1.0";
    public const string FireTvFeature = "amazon.hardware.fire_tv";
    public const int TargetCssWidth = 1920;
    public const int TenFootCssWidthMin = 1280;
    public const int TenFootCssWidthMax = 2100;

    public static bool MatchesAndroidTelevision(
        bool uiModeTelevision,
        bool hasLeanback,
        bool hasFireTvFeature,
        string? model)
    {
        if (uiModeTelevision || hasLeanback || hasFireTvFeature)
            return true;

        return IsFireTvModel(model);
    }

    public static bool IsFireTvModel(string? model)
    {
        if (string.IsNullOrEmpty(model))
            return false;

        // Amazon Fire TV device codes start with AFT (AFTKA, AFTKM, AFTT, ...).
        return model.StartsWith("AFT", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UserAgentLooksLikeTelevision(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return false;

        if (userAgent.Contains(UserAgentMarker, StringComparison.Ordinal))
            return true;

        if (userAgent.Contains("Android TV", StringComparison.OrdinalIgnoreCase))
            return true;

        return ContainsFireTvModelToken(userAgent);
    }

    /// <summary>
    /// TV UI is designed around 1920 CSS px. <c>initial-scale = 1/dpr</c> maps
    /// 1 CSS px to 1 physical px, which on a 4K framebuffer becomes 3840 CSS px
    /// and the 10-foot layout looks tiny. Skip when the CSS width is already
    /// in the 10-foot band (typical 1080p UI, including 4K HDMI with a 1080p
    /// compositor). Zoom out when density reports a phone-sized CSS width.
    /// Zoom in when the CSS width is a 4K physical pixel grid.
    /// </summary>
    public static bool TryGetViewportScale(double cssWidth, out double scale)
    {
        scale = 1;
        if (cssWidth <= 0)
            return false;

        if (cssWidth >= TenFootCssWidthMin && cssWidth <= TenFootCssWidthMax)
            return false;

        scale = cssWidth / TargetCssWidth;
        return Math.Abs(scale - 1) > 0.04;
    }

    private static bool ContainsFireTvModelToken(string userAgent)
    {
        var index = 0;
        while (index < userAgent.Length)
        {
            var found = userAgent.IndexOf("AFT", index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
                return false;

            if (found > 0 && IsTokenChar(userAgent[found - 1]))
            {
                index = found + 3;
                continue;
            }

            var after = found + 3;
            if (after < userAgent.Length && IsTokenChar(userAgent[after]))
                return true;

            index = found + 3;
        }

        return false;
    }

    private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c);
}
