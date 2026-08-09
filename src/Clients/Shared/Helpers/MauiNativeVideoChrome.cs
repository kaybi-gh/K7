namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Native XAML video chrome for MAUI Android/iOS (MediaElement). Windows stays on Blazor + Video.js.
/// </summary>
public static class MauiNativeVideoChrome
{
    /// <summary>
    /// True when the MAUI host should use <c>NativeVideoPlayerOverlay</c> instead of Blazor HUD.
    /// Always false on Windows (Video.js + Blazor controls).
    /// </summary>
    public static bool IsEnabled { get; private set; }

    public static void EnableForNativeMediaElementHosts()
    {
        // Windows MAUI decodes with Video.js in WebView2 and keeps the full Blazor overlay.
        IsEnabled = !OperatingSystem.IsWindows();
    }
}
