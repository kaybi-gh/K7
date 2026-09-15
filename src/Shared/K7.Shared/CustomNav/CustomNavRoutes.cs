using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Shared.CustomNav;

public static class CustomNavRoutes
{
    public static readonly IReadOnlyList<CustomNavAppRoute> All =
    [
        new("/search", "magnifying-glass", "RouteSearch"),
        new("/my-space", "folder-user", "RouteMySpace", CardColor: "#402850"),
        new("/my-space/playlists", "queue", "RoutePlaylists", CardColor: "#501450"),
        new("/my-space/collections", "bookmark-simple", "RouteCollections", CardColor: "#143C50"),
        new("/my-space/stats", "chart-bar", "RouteStats", CardColor: "#145028"),
        new("/my-space/history", "clock-counter-clockwise", "RouteHistory", CardColor: "#503214"),
        new("/my-space/reviews", "chat-circle-text", "RouteReviews", CardColor: "#503C14"),
        new("/my-space/downloads", "download-simple", "RouteDownloads", CardColor: "#141450", NativeOnly: true),
        new("/settings", "gear", "RouteSettings"),
        new("/settings/libraries", "books", "RouteLibraries"),
        new("/settings/home-layout", "house", "RouteHomeSettings"),
        new("/users", "users", "RouteUsers"),
        new("/admin/dashboard", "gauge", "RouteAdminDashboard", true),
        new("/admin/libraries", "video-camera", "RouteAdminLibraries", true),
        new("/admin/library-groups", "squares-four", "RouteAdminLibraryGroups", true),
        new("/admin/users", "users", "RouteAdminUsers", true),
        new("/admin/devices", "devices", "RouteAdminDevices", true),
        new("/admin/restrictions", "shield", "RouteAdminRestrictions", true),
        new("/admin/authentication", "lock", "RouteAdminAuthentication", true),
        new("/admin/general", "gear", "RouteAdminGeneral", true),
        new("/admin/home-layout", "house", "RouteAdminHomeLayout", true),
        new("/admin/navigation", "navigation-arrow", "RouteAdminNavigation", true),
        new("/admin/audio-playback", "music-notes", "RouteAdminAudioPlayback", true),
        new("/admin/video-playback", "play", "RouteAdminVideoPlayback", true),
        new("/admin/transcoding", "film-strip", "RouteAdminTranscoding", true),
        new("/admin/playback-history", "clock-counter-clockwise", "RouteAdminPlaybackHistory", true),
        new("/admin/stats", "chart-bar", "RouteAdminStats", true),
        new("/admin/background-tasks", "queue", "RouteAdminBackgroundTasks", true),
        new("/admin/diagnostics", "stethoscope", "RouteAdminDiagnostics", true),
        new("/admin/federation", "globe", "RouteAdminFederation", true),
        new("/admin/notifications", "bell", "RouteAdminNotifications", true),
        new("/admin/scrobbling", "broadcast", "RouteAdminScrobbling", true),
        new("/admin/api-keys", "key", "RouteAdminApiKeys", true),
        new("/admin/music-intelligence", "brain", "RouteAdminMusicIntelligence", true)
    ];

    public static bool IsMySpacePath(string? route) =>
        !string.IsNullOrWhiteSpace(route)
        && route.StartsWith("/my-space", StringComparison.OrdinalIgnoreCase);

    public static bool IsAvailableForClient(CustomNavAppRoute route, bool isNativeClient) =>
        !route.NativeOnly || isNativeClient;

    public static bool IsItemAvailableForClient(CustomNavItemDto item, bool isNativeClient)
    {
        if (item.Kind is not (CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute))
            return true;

        var route = Find(item.Route);
        return route is null || IsAvailableForClient(route, isNativeClient);
    }

    public static IReadOnlyList<CustomNavItemDto> FilterForClient(
        IEnumerable<CustomNavItemDto> items,
        bool isNativeClient) =>
        items.Where(item => IsItemAvailableForClient(item, isNativeClient)).ToList();

    public static bool TryNormalize(string? route, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(route))
            return false;

        var raw = route.Trim();
        if (raw.Contains('\\', StringComparison.Ordinal)
            || raw.Contains("://", StringComparison.Ordinal)
            || raw.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (!raw.StartsWith('/'))
            raw = "/" + raw;

        var path = raw.Split('?', 2)[0].TrimEnd('/');
        if (path.Length == 0)
            path = "/";
        if (string.Equals(path, "/settings/home", StringComparison.OrdinalIgnoreCase))
            path = "/settings/home-layout";
        if (string.Equals(path, "/admin", StringComparison.OrdinalIgnoreCase))
            path = "/admin/dashboard";

        if (path.Contains("//", StringComparison.Ordinal) || path.Length > CustomNavLimits.MaxRouteLength)
            return false;

        var match = All.FirstOrDefault(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return false;

        normalized = match.Path;
        return true;
    }

    public static CustomNavAppRoute? Find(string? route) =>
        TryNormalize(route, out var path)
            ? All.FirstOrDefault(r => r.Path == path)
            : null;

    public static string? NormalizeBrowseQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query.Trim().TrimStart('?');
        if (trimmed.Length == 0 || trimmed.Length > CustomNavLimits.MaxBrowseQueryLength)
            return null;

        var kept = new List<string>();
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Trim();
            if (!CustomNavLimits.BrowseQueryKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                continue;

            var value = parts.Length > 1 ? parts[1] : "";
            if (string.IsNullOrEmpty(value))
                continue;

            kept.Add($"{key}={value}");
        }

        if (kept.Count == 0)
            return null;

        var joined = string.Join("&", kept);
        return joined.Length > CustomNavLimits.MaxBrowseQueryLength ? null : joined;
    }
}
