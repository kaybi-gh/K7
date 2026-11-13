namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Pairs each personal settings page with the admin page that holds its server defaults, so a
/// page can link to its counterpart and keep the sub-page that is open.
/// </summary>
public static class SettingsMirror
{
    private static readonly (string Settings, string Admin)[] Pairs =
    [
        ("/settings/interface", "/admin/interface"),
        ("/settings/audio-player", "/admin/audio-playback"),
        ("/settings/video-playback", "/admin/video-playback"),
        ("/settings/libraries", "/admin/libraries"),
        ("/settings/scrobbling", "/admin/scrobbling")
    ];

    /// <summary>
    /// Counterpart of <paramref name="path"/> (base-relative, starting with a slash), or null
    /// when the page has none. <paramref name="toAdmin"/> tells which side the link leads to.
    /// </summary>
    public static string? GetCounterpart(string path, out bool toAdmin)
    {
        foreach (var (settings, admin) in Pairs)
        {
            if (TrySwap(path, settings, admin, out var adminPath))
            {
                toAdmin = true;
                return adminPath;
            }

            if (TrySwap(path, admin, settings, out var settingsPath))
            {
                toAdmin = false;
                return settingsPath;
            }
        }

        toAdmin = false;
        return null;
    }

    private static bool TrySwap(string path, string from, string to, out string result)
    {
        if (path.Equals(from, StringComparison.OrdinalIgnoreCase))
        {
            result = to;
            return true;
        }

        if (path.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
        {
            result = to + path[from.Length..];
            return true;
        }

        result = "";
        return false;
    }
}
