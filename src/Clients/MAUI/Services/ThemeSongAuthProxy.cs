using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// Lets the WebView audio element stream theme songs without a token in the URL: it loads an
/// intercepted host, and the interception replays the request against the K7 server through the
/// app HttpClient, which carries the bearer header. Wired on Android, where the WebView raises
/// <see cref="WebViewWebResourceRequestedEventArgs"/> for media element requests.
/// </summary>
public sealed class ThemeSongAuthProxy : IThemeSongSourceResolver
{
    public const string Host = "https://k7-theme-proxy/";

    public string? ResolvePlayableUrl(Guid mediaId, string themeUrl) => $"{Host}{mediaId:D}";

    public static bool TryHandle(WebViewWebResourceRequestedEventArgs e, IK7ServerService serverService)
    {
        var url = e.Uri?.ToString();
        if (url is null || !url.StartsWith(Host, StringComparison.OrdinalIgnoreCase))
            return false;

        e.Handled = true;

        if (!Guid.TryParse(url[Host.Length..].TrimEnd('/'), out var mediaId))
        {
            e.SetResponse(404, "Not Found");
            return true;
        }

        // Status and headers are sent before the server answers; a failed fetch yields an empty
        // body, ambientTheme.js gets an empty blob, and the theme falls back to the byte download.
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "audio/mpeg",
            ["Cache-Control"] = "no-store",
            // The page (app origin) reads it with fetch(): the proxy host is another origin.
            ["Access-Control-Allow-Origin"] = "*"
        };
        e.SetResponse(200, "OK", headers, FetchThemeAsync(serverService, mediaId));
        return true;
    }

    private static async Task<Stream?> FetchThemeAsync(IK7ServerService serverService, Guid mediaId)
    {
        try
        {
            var response = await serverService.HttpClient.GetAsync(
                $"api/medias/{mediaId}/theme",
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return Stream.Null;
            }

            // Buffered: the WebView media stack probes MP3 files by seeking, and a forward-only
            // network stream made playback fail on an Android TV box (2026-11-01 traces). The
            // bytes still stay native: nothing crosses the .NET to JS bridge.
            var bytes = await response.Content.ReadAsByteArrayAsync();
            response.Dispose();
            return new MemoryStream(bytes, writable: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            return Stream.Null;
        }
    }
}
