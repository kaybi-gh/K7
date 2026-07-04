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

    private static Guid? ExtractGroupId(NavigationManager navigation)
    {
        var path = navigation.ToAbsoluteUri(navigation.Uri).AbsolutePath;
        const string prefix = "/library-groups/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var idSegment = path[prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Guid.TryParse(idSegment, out var groupId) ? groupId : null;
    }

    private static bool UriEquals(NavigationManager navigation, string nextUri)
    {
        var current = navigation.ToAbsoluteUri(navigation.Uri);
        var next = navigation.ToAbsoluteUri(nextUri);
        return string.Equals(current.PathAndQuery, next.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }
}
