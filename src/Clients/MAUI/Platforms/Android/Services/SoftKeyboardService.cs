using Android.Content;
using Android.Views.InputMethods;
using K7.Clients.Shared.Interfaces;
using K7.Clients.MAUI.Platforms.Android;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public sealed class SoftKeyboardService : ISoftKeyboardService
{
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
        webView.Post(() => imm.ShowSoftInput(webView, ShowFlags.Implicit));
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
