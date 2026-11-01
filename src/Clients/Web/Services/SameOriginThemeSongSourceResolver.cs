using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Web.Services;

/// <summary>
/// The WASM client is served by the K7 server and authenticates API calls with its cookie.
/// The browser sends that cookie with same-origin media requests, so the audio element can
/// stream the theme URL as is.
/// </summary>
public sealed class SameOriginThemeSongSourceResolver : IThemeSongSourceResolver
{
    public string? ResolvePlayableUrl(Guid mediaId, string themeUrl) => themeUrl;
}
