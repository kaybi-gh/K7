using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Helpers;

internal static class CustomNavPath
{
    public static string From(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);
        if (cut >= 0)
            relative = relative[..cut];

        relative = relative.Trim('/');
        return relative.Length == 0 ? "/" : "/" + relative;
    }

    public static bool ShouldShowBar(CustomNavLayoutDto layout, string path, DeviceType device)
    {
        if (CustomNavVisibility.ShouldShowBar(layout, path, device))
            return true;

        return CustomNavVisibility.HasItems(layout)
            && layout.Placement == CustomNavPlacement.Bar
            && CustomNavVisibility.MatchesDevice(layout, device)
            && layout.ShowBarOnSettings
            && !CustomNavVisibility.IsPhone(device)
            && IsAdminPath(path);
    }

    internal static bool IsAdminPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Trim();
        var cut = normalized.IndexOfAny(['?', '#']);
        if (cut >= 0)
            normalized = normalized[..cut];
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("admin", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
