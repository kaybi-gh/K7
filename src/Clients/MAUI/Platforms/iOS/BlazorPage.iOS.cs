#if IOS
using WebKit;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    // iOS BlazorWebView is backed by a WKWebView. Mirrors the Android/Windows
    // TryEvaluateWebViewJs so shared native-video/overlay code compiles for iOS.
    internal bool TryEvaluateWebViewJs(string script)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not WKWebView webView)
                return false;

            _ = webView.EvaluateJavaScriptAsync(script);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
#endif
