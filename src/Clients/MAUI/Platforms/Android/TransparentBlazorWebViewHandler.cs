#if ANDROID
using Android.Webkit;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace K7.Clients.MAUI.Platforms.Android;

public class TransparentBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(global::Android.Webkit.WebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
    }
}
#endif
