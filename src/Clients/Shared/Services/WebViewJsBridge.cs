using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Holds the active Blazor WebView IJSRuntime for singleton services that must probe
/// Chromium playback capabilities (Windows MAUI).
/// </summary>
public sealed class WebViewJsBridge
{
    private IJSRuntime? _jsRuntime;

    public void SetRuntime(IJSRuntime jsRuntime) => _jsRuntime = jsRuntime;

    public async Task<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken = default)
    {
        if (_jsRuntime is null)
            throw new InvalidOperationException("WebView JS runtime is not available yet.");

        return await _jsRuntime.InvokeAsync<T>(identifier, cancellationToken);
    }
}
