namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Native XAML video chrome for MAUI Android/iOS/Windows. Web WASM stays on Blazor + Video.js.
/// </summary>
public static class MauiNativeVideoChrome
{
    /// <summary>
    /// True when the MAUI host should use <c>NativeVideoPlayerOverlay</c> instead of Blazor HUD.
    /// </summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>
    /// True while native video owns the screen. FeedHub / Home / WebView JS must not
    /// re-render: those patches still hit the UI thread and drop decode frames.
    /// </summary>
    public static bool BackgroundUiPaused { get; private set; }

    public static void EnableForNativeMediaElementHosts()
    {
        IsEnabled = true;
    }

    public static void SetBackgroundUiPaused(bool paused) => BackgroundUiPaused = paused;
}
