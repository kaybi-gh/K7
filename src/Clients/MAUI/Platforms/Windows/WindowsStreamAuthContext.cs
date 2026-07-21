#if WINDOWS
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Shares the current bearer token with WebView2 so Video.js HLS requests can authenticate.
/// </summary>
internal static class WindowsStreamAuthContext
{
    internal static Uri? ServerBaseUri { get; set; }

    internal static string? AuthorizationHeader { get; set; }

    internal static HttpClient? HttpClient { get; set; }

    internal static void Update(Uri? serverBaseUri, string? authorizationHeader, HttpClient? httpClient)
    {
        ServerBaseUri = serverBaseUri;
        AuthorizationHeader = authorizationHeader;
        HttpClient = httpClient;
    }

    internal static void UpdateFrom(IK7ServerService serverService) =>
        Update(
            serverService.HttpClient.BaseAddress,
            serverService.HttpClient.DefaultRequestHeaders.Authorization?.ToString(),
            serverService.HttpClient);
}
#endif
