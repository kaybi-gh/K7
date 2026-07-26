namespace K7.Server.Infrastructure.Configuration;

/// <summary>
/// Shared markers and return-URL helpers for explicit OIDC account linking.
/// </summary>
public static class OidcLinkHelper
{
    public const string LinkMarkerKey = "oidc_link";
    public const string LinkMarkerValue = "1";
    public const string PendingQuery = "oidcLinkPending=1";

    public static bool IsLinkRequest(IDictionary<string, string?>? items, string returnUrl) =>
        (items is not null
            && items.TryGetValue(LinkMarkerKey, out var value)
            && value == LinkMarkerValue)
        || returnUrl.Contains(PendingQuery, StringComparison.OrdinalIgnoreCase);

    public static string BuildResultUrl(string? returnUrl, string result)
    {
        var path = NormalizeReturnPath(returnUrl);
        path = StripQueryKey(path, "oidcLinkPending");
        path = StripQueryKey(path, "oidcLink");
        var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{path}{separator}oidcLink={Uri.EscapeDataString(result)}";
    }

    public static string BuildPendingUrl(string? returnUrl)
    {
        var path = NormalizeReturnPath(returnUrl);
        path = StripQueryKey(path, "oidcLink");
        path = StripQueryKey(path, "oidcLinkPending");
        var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{path}{separator}{PendingQuery}";
    }

    public static string NormalizeReturnPath(string? returnUrl) =>
        IsSafeLocalUrl(returnUrl) ? returnUrl! : "/settings/account";

    public static bool IsSafeLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal);

    public static string StripQueryKey(string path, string key)
    {
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
            return path;

        var basePath = path[..queryIndex];
        var query = path[(queryIndex + 1)..];
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(part, key, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return parts.Length == 0 ? basePath : $"{basePath}?{string.Join('&', parts)}";
    }
}
