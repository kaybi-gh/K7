using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private NativeVideoPlayerOverlay? _nativeOverlay;

#if ANDROID || IOS
    private bool _nativeVideoWebViewShellSaved;
    private bool _savedBlazorWebViewIsVisible = true;
    private double _savedBlazorWebViewOpacity = 1;
    private bool _savedBlazorWebViewInputTransparent;
#endif

    private void InitializeNativeVideoOverlay()
    {
        if (!MauiNativeVideoChrome.IsEnabled || _nativeOverlay is not null)
            return;

        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        _nativeOverlay = new NativeVideoPlayerOverlay(
            _playerService,
            services.GetRequiredService<IDeviceService>(),
            services.GetService<IMediaService>(),
            services.GetService<IUserPreferencesService>(),
            services.GetService<ISyncPlayService>(),
            services.GetService<ICastOrchestrationService>(),
            services.GetService<ICastService>(),
            services.GetService<IBrightnessService>(),
            services.GetService<IVolumeService>(),
            services.GetService<PlaybackProgressTracker>(),
            services.GetService<IK7ServerService>(),
            services.GetService<IFeatureAccessService>(),
            services.GetService<IDeviceStorageService>(),
            services.GetService<K7HubClient>(),
            services.GetService<IRemoteControlService>());

        _nativeOverlay.ZIndex = 5;
        _nativeOverlay.HorizontalOptions = LayoutOptions.Fill;
        _nativeOverlay.VerticalOptions = LayoutOptions.Fill;
        RootGrid.Children.Add(_nativeOverlay);
        NativePlayerCloseButton.IsVisible = false;
    }

    private void OnNativeVideoVisibilityChanged(bool visible)
    {
        if (!MauiNativeVideoChrome.IsEnabled)
            return;

        InitializeNativeVideoOverlay();
        _nativeOverlay?.SetActive(visible);
        NativePlayerCloseButton.IsVisible = false;

#if ANDROID || IOS
        if (visible)
            HideBlazorWebViewForNativeVideo();
        else
            RestoreBlazorWebViewAfterNativeVideo();
#endif
    }

#if ANDROID || IOS
    private void HideBlazorWebViewForNativeVideo()
    {
        if (_nativeVideoWebViewShellSaved)
            return;

        _savedBlazorWebViewIsVisible = blazorWebView.IsVisible;
        _savedBlazorWebViewOpacity = blazorWebView.Opacity;
        _savedBlazorWebViewInputTransparent = blazorWebView.InputTransparent;
        _nativeVideoWebViewShellSaved = true;

        blazorWebView.Opacity = 0;
        blazorWebView.InputTransparent = true;
        blazorWebView.IsVisible = false;
#if ANDROID
        SuppressWebViewFocusForNativeChrome();
#endif
    }

    private void RestoreBlazorWebViewAfterNativeVideo()
    {
        if (!_nativeVideoWebViewShellSaved)
            return;

        // Keep Opacity 0 until Play focus is restored so the casting carousel flash is not visible.
        blazorWebView.IsVisible = _savedBlazorWebViewIsVisible;
        blazorWebView.Opacity = 0;
        blazorWebView.InputTransparent = _savedBlazorWebViewInputTransparent;
        var targetOpacity = _savedBlazorWebViewOpacity;
        _nativeVideoWebViewShellSaved = false;
#if ANDROID
        try
        {
            if (blazorWebView.Handler?.PlatformView is global::Android.Webkit.WebView webView)
            {
                webView.Focusable = true;
                webView.FocusableInTouchMode = true;
            }
        }
        catch
        {
        }

        _ = RevealWebViewAfterFocusRestoreAsync(targetOpacity);
#else
        blazorWebView.Opacity = targetOpacity;
#endif
    }
#endif

#if ANDROID
    private async Task RevealWebViewAfterFocusRestoreAsync(double targetOpacity)
    {
        try
        {
            // Let IsVisible apply, restore hero focus while still invisible, then fade in.
            await Task.Delay(32);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = TryEvaluateWebViewJs(
                    "try{"
                    + "if(window.K7&&K7.reInitAndRestoreCarousels)K7.reInitAndRestoreCarousels();"
                    + "if(window.K7&&K7.restoreFocusAfterNativeVideo)K7.restoreFocusAfterNativeVideo();"
                    + "}catch(e){}");
            });
            await Task.Delay(48);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = TryEvaluateWebViewJs(
                    "try{"
                    + "if(window.K7&&K7.reInitAndRestoreCarousels)K7.reInitAndRestoreCarousels();"
                    + "if(window.K7&&K7.restoreFocusAfterNativeVideo)K7.restoreFocusAfterNativeVideo();"
                    + "}catch(e){}");
                blazorWebView.Opacity = targetOpacity;
            });
            await Task.Delay(160);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = TryEvaluateWebViewJs(
                    "try{if(window.K7&&K7.restoreFocusAfterNativeVideo)K7.restoreFocusAfterNativeVideo();}catch(e){}");
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() => blazorWebView.Opacity = targetOpacity);
        }
    }
#endif

    internal bool TryHandleNativeVideoBack()
    {
        if (!MauiNativeVideoChrome.IsEnabled || !_playerService.IsVisible || _nativeOverlay is null)
            return false;

        return _nativeOverlay.HandleBack();
    }

    internal bool TryHandleNativeVideoKey(string key, bool isKeyUp = false)
    {
        if (!MauiNativeVideoChrome.IsEnabled || !_playerService.IsVisible || _nativeOverlay is null)
            return false;

        return _nativeOverlay.HandleKey(key, isKeyUp);
    }
}
