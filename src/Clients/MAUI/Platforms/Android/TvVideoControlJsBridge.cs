#if ANDROID
using Android.Webkit;
using Java.Interop;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Direct WebView -&gt; native video control. Bypasses the Blazor circuit so seek/back
/// still work after scrub storms stall DotNet interop.
/// </summary>
public sealed class TvVideoControlJsBridge : Java.Lang.Object
{
    public const string InterfaceName = "K7TvVideo";

    [JavascriptInterface]
    [Export("seek")]
    public void Seek(double seconds)
    {
        var page = ResolveBlazorPage();
        page?.SeekFromTvJs(seconds);
    }

    [JavascriptInterface]
    [Export("seekBy")]
    public void SeekBy(double deltaSeconds)
    {
        var page = ResolveBlazorPage();
        page?.SeekByFromTvJs(deltaSeconds);
    }

    /// <summary>Short-press skip using video SkipBack/SkipForward preferences (direction -1 or +1).</summary>
    [JavascriptInterface]
    [Export("skip")]
    public void Skip(int direction)
    {
        var page = ResolveBlazorPage();
        page?.SkipFromTvJs(direction);
    }

    [JavascriptInterface]
    [Export("closePlayer")]
    public void ClosePlayer()
    {
        var page = ResolveBlazorPage();
        page?.ClosePlayerFromTvJs();
    }

    [JavascriptInterface]
    [Export("syncOverlayHidden")]
    public void SyncOverlayHidden()
    {
        // Best-effort Blazor flag sync; DOM is already hidden by JS.
    }

    private static BlazorPage? ResolveBlazorPage()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
        {
            var window = Microsoft.Maui.Controls.Application.Current.Windows[0];
            return window.Page as BlazorPage;
        }

        return null;
    }
}
#endif
