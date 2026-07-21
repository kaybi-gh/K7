namespace K7.Clients.Shared.Helpers;

/// <summary>
/// WinUI MediaElement lacks K7 fMP4 HLS support and WebView2 blocks transparent overlays.
/// Shared UI uses this thin flag to choose Video.js on Windows MAUI.
/// </summary>
public static class WindowsVideoPlayback
{
    public static bool UsesWebVideoPlayer => OperatingSystem.IsWindows();
}
