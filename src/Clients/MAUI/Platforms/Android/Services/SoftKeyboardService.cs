using Android.Content;
using Android.Views.InputMethods;
using K7.Clients.Shared.Interfaces;
using K7.Clients.MAUI.Platforms.Android;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public sealed class SoftKeyboardService : ISoftKeyboardService
{
    private const int ShowRetryDelayMs = 100;

    public void Show()
    {
        MainThread.BeginInvokeOnMainThread(ShowOnMainThread);
    }

    public void Hide()
    {
        MainThread.BeginInvokeOnMainThread(HideOnMainThread);
    }

    private static void ShowOnMainThread()
    {
        var webView = AndroidWebViewAccessor.Current;
        if (webView is null)
            return;

        var context = webView.Context;
        if (context is null)
            return;

        var imm = context.GetSystemService(Context.InputMethodService) as InputMethodManager;
        if (imm is null)
            return;

        // Do not call RequestFocus on the WebView: it blurs the focused DOM input,
        // which triggers navigation.js edit-mode teardown and SpatialNavigation resume.
        // Forced: Implicit often no-ops on TV WebView when the engine has not yet
        // treated the focused DOM node as an editable field.
        void ShowOnce() => imm.ShowSoftInput(webView, ShowFlags.Forced);

        webView.Post(ShowOnce);
        webView.PostDelayed(ShowOnce, ShowRetryDelayMs);
    }

    private static void HideOnMainThread()
    {
        var webView = AndroidWebViewAccessor.Current;
        if (webView is null)
            return;

        var context = webView.Context;
        if (context is null)
            return;

        var imm = context.GetSystemService(Context.InputMethodService) as InputMethodManager;
        imm?.HideSoftInputFromWindow(webView.WindowToken, HideSoftInputFlags.None);
    }
}
