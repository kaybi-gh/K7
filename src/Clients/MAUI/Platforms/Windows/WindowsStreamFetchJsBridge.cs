#if WINDOWS
using System.Text;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Fetches authenticated K7 HLS manifests and segments via HttpClient for Video.js VHS on
/// Windows MAUI, bypassing WebView2 resource interception (BlazorWebViewHandler breaks HLS).
/// </summary>
public sealed class WindowsStreamFetchJsBridge(IK7ServerService serverService) : IWindowsStreamFetchJsBridge, IDisposable
{
    private DotNetObjectReference<WindowsStreamFetchJsBridge>? _ref;

    public async Task RegisterAsync(IJSRuntime js)
    {
        if (_ref is not null)
            return;

        _ref = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("K7.initWindowsStreamFetchBridge", _ref);
    }

    [JSInvokable]
    public async Task<StreamFetchResponse?> FetchStreamAsync(string url, string? rangeHeader)
    {
        var httpClient = serverService.HttpClient;
        if (httpClient.BaseAddress is not Uri serverBaseUri
            || !Uri.TryCreate(url, UriKind.Absolute, out var requestUri))
        {
            return null;
        }

        if (!requestUri.AbsoluteUri.StartsWith(serverBaseUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)
            || !HlsStreamUrlHelper.IsK7StreamResource(requestUri.AbsoluteUri))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (!string.IsNullOrEmpty(rangeHeader))
                request.Headers.TryAddWithoutValidation("Range", rangeHeader);

            System.Diagnostics.Debug.WriteLine(
                "[K7-Player] Stream fetch via C# url=" + requestUri);

            using var response = await httpClient.SendAsync(request);
            var bodyBytes = await response.Content.ReadAsByteArrayAsync();
            bodyBytes = PrepareBodyBytes(bodyBytes, requestUri);

            var contentType = response.Content.Headers.ContentType?.MediaType
                ?? GetFallbackContentType(requestUri);

            System.Diagnostics.Debug.WriteLine(
                "[K7-Player] Stream fetch response status="
                + (int)response.StatusCode
                + " url="
                + requestUri
                + " bytes="
                + bodyBytes.Length);

            return new StreamFetchResponse((int)response.StatusCode, contentType, bodyBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "[K7-Player] Stream fetch failed url="
                + requestUri
                + " error="
                + ex.Message
                + " type="
                + ex.GetType().Name);
            throw;
        }
    }

    private static byte[] PrepareBodyBytes(byte[] bodyBytes, Uri requestUri)
    {
        if (bodyBytes.Length == 0)
            return bodyBytes;

        if (!requestUri.AbsoluteUri.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            return bodyBytes;

        var manifestText = Encoding.UTF8.GetString(bodyBytes);
        var rewritten = HlsStreamUrlHelper.AbsolutizeManifestUrls(manifestText, requestUri);
        var previewLength = Math.Min(rewritten.Length, 200);
        var preview = rewritten[..previewLength].Replace("\r", "\\r").Replace("\n", "\\n");

        System.Diagnostics.Debug.WriteLine(
            "[K7-Player] Stream fetch manifest preview=" + preview);

        return Encoding.UTF8.GetBytes(rewritten);
    }

    private static string GetFallbackContentType(Uri requestUri)
    {
        var url = requestUri.AbsoluteUri;
        if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.apple.mpegurl";

        if (url.Contains(".vtt", StringComparison.OrdinalIgnoreCase))
            return "text/vtt";

        return "application/octet-stream";
    }

    public void Dispose() => _ref?.Dispose();

    public sealed class StreamFetchResponse(int statusCode, string contentType, byte[] body)
    {
        public int StatusCode { get; } = statusCode;

        public string ContentType { get; } = contentType;

        public byte[] Body { get; } = body;
    }
}
#endif
