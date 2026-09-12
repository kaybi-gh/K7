using System.Text.Json;

namespace K7.Server.Application.Features.Notifications;

internal static class NotificationWebhookUrl
{
    public static bool IsHttpOrHttps(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool ProviderConfigHasHttpUrl(string? providerConfig)
    {
        if (string.IsNullOrWhiteSpace(providerConfig))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(providerConfig);
            if (!doc.RootElement.TryGetProperty("url", out var urlProp)
                || urlProp.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return IsHttpOrHttps(urlProp.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
