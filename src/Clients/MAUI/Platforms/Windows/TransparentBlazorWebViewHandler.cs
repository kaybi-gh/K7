#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Web.WebView2.Core;
using Color = Windows.UI.Color;

namespace K7.Clients.MAUI.Platforms.Windows;

public class TransparentBlazorWebViewHandler : BlazorWebViewHandler
{
    protected override void ConnectHandler(WebView2 platformView)
    {
        base.ConnectHandler(platformView);

        platformView.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

        platformView.CoreWebView2Initialized += (_, _) =>
        {
            var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "downloads");
            Directory.CreateDirectory(downloadsPath);
            platformView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "k7-local-files",
                downloadsPath,
                CoreWebView2HostResourceAccessKind.Allow);
        };
    }
}
#endif
