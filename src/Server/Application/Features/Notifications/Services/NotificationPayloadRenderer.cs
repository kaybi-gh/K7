using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Notifications.Services;

public partial class NotificationPayloadRenderer
{
    public string Render(string? template, IReadOnlyDictionary<string, object?> eventData)
    {
        if (string.IsNullOrWhiteSpace(template))
            return System.Text.Json.JsonSerializer.Serialize(eventData);

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (eventData.TryGetValue(key, out var value))
            {
                var stringValue = value?.ToString() ?? "";
                // Escape for JSON string context
                return System.Text.Json.JsonEncodedText.Encode(stringValue).ToString();
            }

            return match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(.+?)\}\}")]
    private static partial Regex PlaceholderRegex();
}
