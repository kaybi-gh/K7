using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Helpers;

public static class LibraryGroupBrowseUrlSync
{
    public static bool HasBrowseQuery(NavigationManager navigation)
    {
        foreach (var key in LibraryGroupBrowseNavigationHelper.BrowseQueryKeys)
        {
            if (!string.IsNullOrEmpty(PageFilterUrlSync.GetQueryValue(navigation, key)))
                return true;
        }

        return false;
    }

    public static LibraryGroupBrowseUrlState ReadState(NavigationManager navigation) =>
        LibraryGroupBrowseNavigationHelper.ParseBrowseState(PageFilterUrlSync.GetQuery(navigation));

    public static string Fingerprint(Guid groupId, LibraryGroupBrowseUrlState state) =>
        LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(groupId, state);

    public static string Fingerprint(NavigationManager navigation)
    {
        var groupId = ExtractGroupId(navigation);
        return groupId is null ? "" : Fingerprint(groupId.Value, ReadState(navigation));
    }

    public static Guid? ExtractGroupId(NavigationManager navigation)
    {
        var path = navigation.ToAbsoluteUri(navigation.Uri).AbsolutePath;
        return ExtractGroupId(path);
    }

    public static Guid? ExtractGroupId(string path)
    {
        const string prefix = "/library-groups/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var idSegment = path[prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Guid.TryParse(idSegment, out var groupId) ? groupId : null;
    }

    public static void Navigate(NavigationManager navigation, string href)
    {
        var current = navigation.ToAbsoluteUri(navigation.Uri);
        var target = navigation.ToAbsoluteUri(href);
        var next = MergeBrowseHref(current, target);
        if (string.Equals(current.PathAndQuery, next, StringComparison.OrdinalIgnoreCase))
            return;

        navigation.NavigateTo(next);
    }

    public static string MergeBrowseHref(Uri current, Uri target)
    {
        if (!string.Equals(current.AbsolutePath, target.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            return target.PathAndQuery;

        var query = ParseQuery(current.Query);
        foreach (var key in LibraryGroupBrowseNavigationHelper.BrowseQueryKeys)
            query.Remove(key);

        foreach (var (key, value) in ParseQuery(target.Query))
            query[key] = value;

        if (query.Count == 0)
            return target.AbsolutePath;

        var qs = string.Join("&", query.Select(pair =>
            string.IsNullOrEmpty(pair.Value)
                ? Uri.EscapeDataString(pair.Key)
                : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{target.AbsolutePath}?{qs}";
    }

    public static void SyncState(NavigationManager navigation, LibraryGroupBrowseUrlState state)
    {
        var groupId = ExtractGroupId(navigation);
        if (groupId is null)
            return;

        var targetUrl = LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(groupId.Value, state);
        if (!UriEquals(navigation, targetUrl))
            navigation.NavigateTo(targetUrl, replace: true);
    }

    public static void SyncAfterRender(
        NavigationManager navigation,
        bool firstRender,
        ref bool pending,
        LibraryGroupBrowseUrlState state)
    {
        if (!firstRender || !pending)
            return;

        pending = false;
        SyncState(navigation, state);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
            return result;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
                continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            result[key] = value;
        }

        return result;
    }

    private static bool UriEquals(NavigationManager navigation, string nextUri)
    {
        var current = navigation.ToAbsoluteUri(navigation.Uri);
        var next = navigation.ToAbsoluteUri(nextUri);
        return string.Equals(current.PathAndQuery, next.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }
}
