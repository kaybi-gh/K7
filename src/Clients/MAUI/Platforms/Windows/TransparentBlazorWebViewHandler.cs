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
        // Inject Authorization on other server API requests from WebView2.
        // HLS manifests/segments are fetched via the JS stream bridge (Video.js VHS xhr).
        platformView.CoreWebView2Initialized += OnCoreWebView2Initialized;

        base.ConnectHandler(platformView);

        // Opaque background: transparent WebView2 passes pointer input through to native
        // MediaElement layers below, which breaks the Blazor video controls overlay.
        platformView.DefaultBackgroundColor = Color.FromArgb(255, 13, 9, 7);
        platformView.IsTabStop = true;
        platformView.IsHitTestVisible = true;
    }

    private static void OnCoreWebView2Initialized(object? sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null || sender is not WebView2 platformView)
            return;

        // Re-apply after init; WinUI may reset to theme brush otherwise.
        platformView.DefaultBackgroundColor = Color.FromArgb(255, 13, 9, 7);
        platformView.IsHitTestVisible = true;

        var coreWebView = platformView.CoreWebView2;
        var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "downloads");
        Directory.CreateDirectory(downloadsPath);
        coreWebView.SetVirtualHostNameToFolderMapping(
            "k7-local-files",
            downloadsPath,
            CoreWebView2HostResourceAccessKind.Allow);

        coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        coreWebView.WebResourceRequested += OnStreamWebResourceRequested;
    }

    private static void OnStreamWebResourceRequested(
        CoreWebView2 sender,
        CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (WindowsStreamAuthContext.ServerBaseUri is not Uri serverBaseUri
            || string.IsNullOrEmpty(WindowsStreamAuthContext.AuthorizationHeader))
        {
            return;
        }

        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var requestUri))
            return;

        if (!requestUri.AbsoluteUri.StartsWith(serverBaseUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            return;

        args.Request.Headers.SetHeader("Authorization", WindowsStreamAuthContext.AuthorizationHeader);
    }
}
#endif
