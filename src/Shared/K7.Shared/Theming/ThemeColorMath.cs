using System.Globalization;

namespace K7.Shared.Theming;

/// <summary>Hex color parsing and WCAG contrast math for theme validation and derivation.</summary>
public static class ThemeColorMath
{
    public const string DarkInk = "#1c1611";
    public const string LightInk = "#fbf7ef";

    public static bool IsHex(string? value)
    {
        if (value is null || value.Length != 7 || value[0] != '#')
            return false;

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
                return false;
        }

        return true;
    }

    public static string Normalize(string hex) => hex.ToLowerInvariant();

    public static double RelativeLuminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    public static double ContrastRatio(string first, string second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Mixes <paramref name="amount"/> (0..1) of <paramref name="other"/> into <paramref name="hex"/>.</summary>
    public static string Mix(string hex, string other, double amount)
    {
        var (r1, g1, b1) = Parse(hex);
        var (r2, g2, b2) = Parse(other);
        amount = Math.Clamp(amount, 0, 1);
        return ToHex(Lerp(r1, r2, amount), Lerp(g1, g2, amount), Lerp(b1, b2, amount));
    }

    /// <summary>Dark or light ink, whichever reads best on <paramref name="fill"/>.</summary>
    public static string ReadableInkOn(string fill) =>
        ContrastRatio(DarkInk, fill) >= ContrastRatio(LightInk, fill) ? DarkInk : LightInk;

    /// <summary>
    /// Returns <paramref name="color"/>, pushed towards black or white until it reaches
    /// <paramref name="minimumRatio"/> against <paramref name="background"/>.
    /// </summary>
    public static string EnsureContrast(string color, string background, double minimumRatio)
    {
        if (ContrastRatio(color, background) >= minimumRatio)
            return Normalize(color);

        var target = RelativeLuminance(background) < 0.5 ? "#ffffff" : "#000000";
        for (var step = 1; step <= 20; step++)
        {
            var candidate = Mix(color, target, step / 20d);
            if (ContrastRatio(candidate, background) >= minimumRatio)
                return candidate;
        }

        return target;
    }

    private static (int R, int G, int B) Parse(string hex)
    {
        if (!IsHex(hex))
            throw new FormatException("Expected a #rrggbb color.");

        return (
            int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double Linear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static int Lerp(int from, int to, double amount) =>
        (int)Math.Round(from + (to - from) * amount, MidpointRounding.AwayFromZero);

    private static string ToHex(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{r:x2}{g:x2}{b:x2}");
}
