namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Windows MAUI: LibVLC for muxed Direct Play; Video.js for HLS transcode (WebView2).
/// Web WASM always uses Video.js. See docs/dev/video-playback.md.
/// </summary>
public static class WindowsVideoPlayback
{
    /// <summary>
    /// True when the current stream should decode in WebView2 Video.js (HLS transcode).
    /// </summary>
    public static bool ShouldUseWebVideoPlayer(string? mimeType, string? url) =>
        StreamingSourceKind.IsHls(mimeType, url);

    /// <summary>
    /// True when the current stream should decode in LibVLC (muxed direct-stream / local file).
    /// </summary>
    public static bool ShouldUseLibVlc(string? mimeType, string? url) =>
        !string.IsNullOrEmpty(url) && !ShouldUseWebVideoPlayer(mimeType, url);
}
