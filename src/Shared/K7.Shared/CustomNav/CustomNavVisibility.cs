using K7.Server.Domain.Enums;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Shared.CustomNav;

public static class CustomNavVisibility
{
    private static readonly string[] BlockedPrefixes =
    [
        "/sign-in",
        "/auth",
        "/select-profile",
        "/select-user",
        "/link",
        "/linkdevice",
        "/not-found",
        "/syncplay"
    ];

    private static readonly string[] MediaPrefixes =
    [
        "/movies",
        "/series",
        "/music",
        "/library-groups",
        "/persons",
        "/search",
        "/playlists",
        "/dynamic-playlists",
        "/collections"
    ];

    public static bool HasItems(CustomNavLayoutDto layout) =>
        layout.Enabled && layout.Items.Count > 0;

    public static bool MatchesDevice(CustomNavLayoutDto layout, DeviceType device) =>
        device switch
        {
            DeviceType.TV => layout.ShowOnTv,
            DeviceType.Phone or DeviceType.Tablet => layout.ShowOnPhone,
            DeviceType.Watch => false,
            _ => layout.ShowOnDesktop
        };

    public static bool IsPhone(DeviceType device) =>
        device is DeviceType.Phone or DeviceType.Tablet;

    public static bool ShouldShowBar(CustomNavLayoutDto layout, string path, DeviceType device) =>
        HasItems(layout)
        && layout.Placement == CustomNavPlacement.Bar
        && MatchesDevice(layout, device)
        && !IsPhone(device)
        && MatchesBarPages(layout, path);

    public static bool ShouldShowHomeRow(CustomNavLayoutDto layout, DeviceType device) =>
        HasItems(layout)
        && MatchesDevice(layout, device)
        && layout.Placement == CustomNavPlacement.FeedRow
        && layout.ShowRowOnHome;

    public static bool ShouldShowGroupFeedRow(CustomNavLayoutDto layout, DeviceType device) =>
        HasItems(layout)
        && MatchesDevice(layout, device)
        && !IsPhone(device)
        && layout.Placement == CustomNavPlacement.FeedRow
        && layout.ShowRowOnExploreFeeds;

    public static bool MatchesBarPages(CustomNavLayoutDto layout, string path)
    {
        var normalized = NormalizePath(path);
        if (MatchesAnyPrefix(normalized, BlockedPrefixes))
            return false;

        if (normalized == "/")
            return layout.ShowBarOnHome;

        if (MatchesPrefix(normalized, "/explore"))
            return layout.ShowBarOnExplore;

        if (MatchesPrefix(normalized, "/my-space"))
            return layout.ShowBarOnMySpace;

        if (IsSettingsOrAdminPath(normalized))
            return layout.ShowBarOnSettings;

        if (MatchesAnyPrefix(normalized, MediaPrefixes))
            return layout.ShowBarOnMedia;

        return false;
    }

    public static bool IsActive(string href, string path)
    {
        var hrefPath = NormalizePath(href.Split('?', 2)[0]);
        var current = NormalizePath(path);
        if (hrefPath == "/")
            return current == "/";

        return current.Equals(hrefPath, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(hrefPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAnyPrefix(string path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (MatchesPrefix(path, prefix))
                return true;
        }

        return false;
    }

    private static bool MatchesPrefix(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    private static bool IsSettingsOrAdminPath(string path)
    {
        if (MatchesPrefix(path, "/settings") || MatchesPrefix(path, "/admin"))
            return true;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("admin", StringComparison.OrdinalIgnoreCase)
                || part.Equals("settings", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var trimmed = path.Trim();
        var queryAt = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (queryAt >= 0)
            trimmed = trimmed[..queryAt];
        if (!trimmed.StartsWith('/'))
            trimmed = "/" + trimmed;

        if (trimmed.Length > 1)
            trimmed = trimmed.TrimEnd('/');

        return trimmed;
    }
}
