using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Services;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

/// <summary>
/// Reports playback capabilities from WebView2 (Chromium MSE), not WinUI MediaElement / Media Foundation.
/// K7 HLS uses fMP4 segments with #EXT-X-MAP; WinUI MediaElement does not support that tag
/// (https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/hls-tag-support).
/// Video playback on Windows uses Video.js in WebView2, so stream-session codec negotiation must match Chromium.
/// </summary>
public class CodecService(WebViewJsBridge jsBridge) : ICodecService
{
    public async Task<bool> GetHdrSupportAsync()
    {
        return await jsBridge.InvokeAsync<bool>("getHdrSupport");
    }

    public async Task<string[]> GetSupportedVideoCodecsAsync()
    {
        var codecs = await jsBridge.InvokeAsync<string[]>("getSupportedVideoCodecsAsync");
        return codecs ?? [];
    }

    public async Task<string[]> GetSupportedAudioCodecsAsync()
    {
        var codecs = await jsBridge.InvokeAsync<string[]>("getSupportedAudioCodecsAsync");
        return codecs ?? [];
    }

    public async Task<string[]> GetSupportedContainersAsync()
    {
        var containers = await jsBridge.InvokeAsync<string[]>("getSupportedContainersAsync");
        return containers ?? [];
    }
}
