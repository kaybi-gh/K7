namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// Gives the ambient theme player a URL its HTML audio element can load with the current
/// authentication (an audio element cannot send the bearer header). Without a resolver, or
/// when it returns null, the theme is downloaded by .NET and handed to JS as bytes.
/// </summary>
public interface IThemeSongSourceResolver
{
    string? ResolvePlayableUrl(Guid mediaId, string themeUrl);
}
