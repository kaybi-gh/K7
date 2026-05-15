#if IOS
using Microsoft.AspNetCore.Components.WebView.Maui;
using UIKit;
using WebKit;

namespace K7.Clients.MAUI.Platforms.iOS;

public class TransparentBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(WKWebView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Opaque = false;
        platformView.BackgroundColor = UIColor.Clear;
        platformView.ScrollView.BackgroundColor = UIColor.Clear;
    }
}
#endif
