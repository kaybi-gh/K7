using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Tells which kind of media page an address shows, so the page gets the settings of its kind.
/// </summary>
public static class MediaPageRoutes
{
    public static MediaPageKind? KindOf(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);
        return KindOf(cut >= 0 ? relative[..cut] : relative);
    }

    /// <summary>Kind of the page at this path, null when it is not a media page with a hero.</summary>
    public static MediaPageKind? KindOf(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return null;

        return segments[0].ToLowerInvariant() switch
        {
            "movies" when segments.Length == 2 => MediaPageKind.Movie,
            "persons" when segments.Length == 2 => MediaPageKind.Person,
            "series" when segments.Length == 2 => MediaPageKind.Series,
            "series" when segments.Length == 4
                && string.Equals(segments[2], "seasons", StringComparison.OrdinalIgnoreCase) => MediaPageKind.Season,
            "series" when segments.Length == 6
                && string.Equals(segments[2], "seasons", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[4], "episodes", StringComparison.OrdinalIgnoreCase) => MediaPageKind.Episode,
            _ => null
        };
    }
}
