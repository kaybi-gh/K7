using System.Text.RegularExpressions;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Route helpers for ambient theme session continuity.
/// </summary>
public static partial class AmbientThemeRoutes
{
    public static bool TryGetThemeMediaId(string absoluteUri, out Guid mediaId)
    {
        mediaId = default;
        if (!TryGetAbsolutePath(absoluteUri, out var path))
            return false;

        var movieMatch = MoviePathRegex().Match(path);
        if (movieMatch.Success)
            return Guid.TryParse(movieMatch.Groups[1].Value, out mediaId);

        var serieMatch = SeriePathRegex().Match(path);
        if (serieMatch.Success)
            return Guid.TryParse(serieMatch.Groups[1].Value, out mediaId);

        return false;
    }

    public static bool IsPersonRoute(string absoluteUri)
    {
        if (!TryGetAbsolutePath(absoluteUri, out var path))
            return false;

        return PersonPathRegex().IsMatch(path);
    }

    /// <summary>
    /// Routes that should keep an active theme context (playing or finished):
    /// media detail tree and person digressions from casting.
    /// </summary>
    public static bool IsThemeHoldingRoute(string absoluteUri) =>
        TryGetThemeMediaId(absoluteUri, out _) || IsPersonRoute(absoluteUri);

    private static bool TryGetAbsolutePath(string absoluteUri, out string path)
    {
        path = "";
        if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out var uri))
            return false;

        path = uri.AbsolutePath.TrimEnd('/');
        return true;
    }

    [GeneratedRegex(@"^/movies/([^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoviePathRegex();

    [GeneratedRegex(@"^/series/([^/]+)(?:/seasons/\d+(?:/episodes/\d+)?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeriePathRegex();

    [GeneratedRegex(@"^/persons/([^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PersonPathRegex();
}
