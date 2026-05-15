#if ANDROID
using AndroidX.Core.View;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace K7.Clients.MAUI.Platforms.Android;

public class TransparentBlazorWebViewHandler : BlazorWebViewHandler
{
    private const string TvUserAgentMarker = "K7TV/1.0";

    protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);

        ApplyTvScalingIfNeeded(platformView);
        SetupSafeAreaInsets(platformView);
    }

    private static void SetupSafeAreaInsets(global::Android.Webkit.WebView webView)
    {
        if (DeviceInfo.Idiom == DeviceIdiom.TV)
            return;

        ViewCompat.SetOnApplyWindowInsetsListener(webView, new SafeAreaInsetsListener(webView));
    }

    private sealed class SafeAreaInsetsListener(global::Android.Webkit.WebView webView)
        : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
                return insets;

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            if (bars is null)
                return insets;

            var density = v.Resources?.DisplayMetrics?.Density ?? 1f;
            var top = (bars.Top / density).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var bottom = (bars.Bottom / density).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var left = (bars.Left / density).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var right = (bars.Right / density).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var js = $"(function(){{var s=document.documentElement.style;" +
                     $"s.setProperty('--k7-safe-top','{top}px');" +
                     $"s.setProperty('--k7-safe-bottom','{bottom}px');" +
                     $"s.setProperty('--k7-safe-left','{left}px');" +
                     $"s.setProperty('--k7-safe-right','{right}px')}})()";
            webView.EvaluateJavascript(js, null);

            return ViewCompat.OnApplyWindowInsets(v, insets);
        }
    }

    private static void ApplyTvScalingIfNeeded(global::Android.Webkit.WebView webView)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.TV)
        {
            return;
        }

        var settings = webView.Settings;
        settings.UseWideViewPort = true;
        settings.LoadWithOverviewMode = true;

        // Tag the User-Agent so the page can detect TV mode synchronously in <head>
        // and rewrite its <meta viewport> to compensate for the high pixel density.
        // We can't rely on WebView.SetInitialScale here: Android ignores it whenever
        // the page declares its own viewport meta tag, which K7's index.html does.
        var currentUa = settings.UserAgentString ?? string.Empty;
        if (!currentUa.Contains(TvUserAgentMarker, System.StringComparison.Ordinal))
        {
            settings.UserAgentString = string.IsNullOrWhiteSpace(currentUa)
                ? TvUserAgentMarker
                : $"{currentUa} {TvUserAgentMarker}";
        }
    }
}
#endif


