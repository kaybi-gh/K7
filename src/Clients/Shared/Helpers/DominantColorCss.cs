namespace K7.Clients.Shared.Helpers;

public static class DominantColorCss
{
    public static string? ToCssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (trimmed.StartsWith('#'))
            return trimmed;

        if (trimmed.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return TryParseRgbComponents(trimmed, out var r, out var g, out var b)
            ? $"rgb({r}, {g}, {b})"
            : null;
    }

    public static string ToVariableStyle(string variableName, string? value)
    {
        var cssColor = ToCssColor(value);
        return cssColor is not null ? $"{variableName}: {cssColor};" : "";
    }

    public static bool TryParseRgbComponents(string? value, out int r, out int g, out int b)
    {
        r = g = b = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3
            && int.TryParse(parts[0], out r)
            && int.TryParse(parts[1], out g)
            && int.TryParse(parts[2], out b)
            && r is >= 0 and <= 255
            && g is >= 0 and <= 255
            && b is >= 0 and <= 255;
    }
}
