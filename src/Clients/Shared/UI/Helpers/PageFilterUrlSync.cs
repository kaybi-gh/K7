using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Helpers;

public static class PageFilterUrlSync
{
    public static bool HasAnyQuery(NavigationManager navigation, params ReadOnlySpan<string> keys)
    {
        var query = ParseQuery(navigation);
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return true;
            }
        }

        return false;
    }

    public static string? GetQueryValue(NavigationManager navigation, string key)
    {
        var query = ParseQuery(navigation);
        return query.TryGetValue(key, out var value) ? value : null;
    }

    public static IReadOnlyDictionary<string, string> GetQuery(NavigationManager navigation) =>
        ParseQuery(navigation);

    public static void SetQuery(NavigationManager navigation, IReadOnlyDictionary<string, string?> parameters)
    {
        var queryParams = new Dictionary<string, object?>(parameters.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parameters)
        {
            queryParams[key] = string.IsNullOrEmpty(value) ? null : value;
        }

        var uri = navigation.GetUriWithQueryParameters(queryParams);
        if (!UriEquals(navigation, uri))
        {
            navigation.NavigateTo(uri, replace: true);
        }
    }

    public static void SyncAfterRender(NavigationManager navigation, bool firstRender, ref bool pending, IReadOnlyDictionary<string, string?> parameters)
    {
        if (!firstRender || !pending)
        {
            return;
        }

        pending = false;
        SetQuery(navigation, parameters);
    }

    private static bool UriEquals(NavigationManager navigation, string nextUri)
    {
        var current = navigation.ToAbsoluteUri(navigation.Uri);
        var next = navigation.ToAbsoluteUri(nextUri);
        return string.Equals(current.PathAndQuery, next.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseQuery(NavigationManager navigation)
    {
        var uri = navigation.ToAbsoluteUri(navigation.Uri);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(uri.Query))
        {
            return result;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            {
                continue;
            }

            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            result[key] = value;
        }

        return result;
    }
}
