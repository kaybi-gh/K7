#if ANDROID
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


