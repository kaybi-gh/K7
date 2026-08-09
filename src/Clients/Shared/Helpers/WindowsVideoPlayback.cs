namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Windows MAUI decodes video with Video.js in WebView2, not WinUI MediaElement.
/// K7 HLS is fMP4 with #EXT-X-MAP, which Media Foundation does not support.
/// See docs/dev/video-playback.md and
/// https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/hls-tag-support
/// </summary>
public static class WindowsVideoPlayback
{
    public static bool UsesWebVideoPlayer => OperatingSystem.IsWindows();
}
