#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Color = Windows.UI.Color;

namespace K7.Clients.MAUI.Platforms.Windows;

public class TransparentBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(WebView2 platformView)
    {
        base.ConnectHandler(platformView);

        platformView.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);
    }
}
#endif
