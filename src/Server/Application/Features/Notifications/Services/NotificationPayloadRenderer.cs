using System.Text.Json;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Notifications.Services;

public partial class NotificationPayloadRenderer
{
    public string Render(string? template, IReadOnlyDictionary<string, object?> eventData) =>
        Render(template, eventData, escapeForJson: true);

    public string RenderPlain(string? template, IReadOnlyDictionary<string, object?> eventData) =>
        Render(template, eventData, escapeForJson: false);

    public string Render(string? template, IReadOnlyDictionary<string, object?> eventData, bool escapeForJson)
    {
        if (string.IsNullOrWhiteSpace(template))
            return JsonSerializer.Serialize(eventData);

        // Triple-brace {{{Name}}} inserts raw (unquoted) values for JSON numbers / booleans.
        var withRaw = RawPlaceholderRegex().Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            if (!TryResolve(expression, eventData, out var resolved))
                return match.Value;

            return resolved;
        });

        return PlaceholderRegex().Replace(withRaw, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            if (!TryResolve(expression, eventData, out var resolved))
                return match.Value;

            return escapeForJson
                ? JsonEncodedText.Encode(resolved).ToString()
                : resolved;
        });
    }

    public static bool TryResolve(
        string expression,
        IReadOnlyDictionary<string, object?> eventData,
        out string resolved)
    {
        resolved = "";
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var parts = expression.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var key = parts[0];
        if (!eventData.TryGetValue(key, out var raw))
            return false;

        var stringValue = FormatValue(raw);
        if (parts.Length == 1)
        {
            resolved = stringValue;
            return true;
        }

        string? fallback = null;
        string? emptyFallback = null;
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var mapKey = part[..eq].Trim();
            var mapValue = part[(eq + 1)..];
            if (mapKey == "*")
            {
                fallback = mapValue;
                continue;
            }

            // empty= applies only when the value is null/blank (JSON null for optional numbers).
            // Do not use *=null for that: * is the unmatched-value default and would replace real numbers.
            if (mapKey == "empty")
            {
                emptyFallback = mapValue;
                continue;
            }

            if (ValuesMatch(mapKey, raw, stringValue))
            {
                resolved = mapValue;
                return true;
            }
        }

        if (string.IsNullOrEmpty(stringValue) && emptyFallback is not null)
        {
            resolved = emptyFallback;
            return true;
        }

        resolved = fallback ?? stringValue;
        return true;
    }

    private static bool ValuesMatch(string mapKey, object? raw, string stringValue)
    {
        if (string.Equals(mapKey, stringValue, StringComparison.OrdinalIgnoreCase))
            return true;

        if (raw is bool boolValue
            && bool.TryParse(mapKey, out var mappedBool)
            && mappedBool == boolValue)
            return true;

        return false;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? ""
    };

    // Match {{{...}}} before {{...}} by requiring three braces.
    [GeneratedRegex(@"\{\{\{(.+?)\}\}\}")]
    private static partial Regex RawPlaceholderRegex();

    [GeneratedRegex(@"\{\{([^{].*?)\}\}")]
    private static partial Regex PlaceholderRegex();
}
