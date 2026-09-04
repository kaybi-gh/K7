using K7.Clients.MAUI.Controls.Video;
#if ANDROID
using K7.Clients.MAUI.Platforms.Android;
#endif
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private NativeVideoPlayerOverlay? _nativeOverlay;
    private IRemoteControlService? _remoteControlForChrome;
    private bool _remoteControlChromeSubscribed;

#if ANDROID || IOS || WINDOWS
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

        _remoteControlForChrome ??= services.GetService<IRemoteControlService>();
        EnsureRemoteControlChromeSubscription();

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
            _remoteControlForChrome);

        _nativeOverlay.ZIndex = 5;
        _nativeOverlay.HorizontalOptions = LayoutOptions.Fill;
        _nativeOverlay.VerticalOptions = LayoutOptions.Fill;
        RootGrid.Children.Add(_nativeOverlay);
        NativePlayerCloseButton.IsVisible = false;
    }

    private void EnsureRemoteControlChromeSubscription()
    {
        if (_remoteControlChromeSubscribed || _remoteControlForChrome is null)
            return;

        _remoteControlForChrome.SessionChanged += OnRemoteControlChromeSessionChanged;
        _remoteControlChromeSubscribed = true;
    }

    private void OnRemoteControlChromeSessionChanged() =>
        MainThread.BeginInvokeOnMainThread(SyncNativeVideoChrome);

    /// <summary>
    /// Local decode chrome only. While remoting video to another device, Blazor owns
    /// <c>RemoteControlPanel</c> and the native overlay must not cover the WebView.
    /// </summary>
    private bool WantsNativeVideoChrome =>
        MauiNativeVideoChrome.IsEnabled
        && _playerService.IsVisible
        && !(_remoteControlForChrome is { IsControlling: true, IsAudio: false });

    private void OnNativeVideoVisibilityChanged(bool visible) => SyncNativeVideoChrome();

    private void SyncNativeVideoChrome()
    {
        if (!MauiNativeVideoChrome.IsEnabled)
            return;

        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services;
        _remoteControlForChrome ??= services?.GetService<IRemoteControlService>();
        EnsureRemoteControlChromeSubscription();

        var showChrome = WantsNativeVideoChrome;
#if WINDOWS
        var source = _playerService.Source;
        var useLibVlcSurface = showChrome
            && (string.IsNullOrEmpty(source?.Url)
                || WindowsVideoPlayback.ShouldUseLibVlc(source.MimeType, source.Url));
#else
        var useLibVlcSurface = showChrome;
#endif

        InitializeNativeVideoOverlay();
        _nativeOverlay?.SetActive(showChrome);
        NativePlayerCloseButton.IsVisible = false;

#if WINDOWS
        SetWindowsVlcSurfaceVisible(showChrome && useLibVlcSurface);
#endif

#if ANDROID || IOS || WINDOWS
        if (!showChrome)
        {
            if (_playerService.IsVisible
                && _remoteControlForChrome is { IsControlling: true, IsAudio: false })
            {
                RevealBlazorWebViewForRemoteControl();
            }
            else
            {
                RestoreBlazorWebViewAfterNativeVideo();
#if ANDROID
                if (!_playerService.IsVisible)
                    AndroidDisplayAfr.Restore();
#endif
            }

            return;
        }

        if (useLibVlcSurface)
        {
            HideBlazorWebViewForNativeVideo();
#if WINDOWS
            BackgroundColor = Colors.Black;
#endif
        }
        else
        {
#if WINDOWS
            ShowBlazorWebViewUnderNativeChrome();
            BackgroundColor = Colors.Black;
#else
            RestoreBlazorWebViewAfterNativeVideo();
#endif
        }
#endif
    }

#if ANDROID || IOS || WINDOWS
    private void HideBlazorWebViewForNativeVideo()
    {
        if (!_nativeVideoWebViewShellSaved)
        {
            _savedBlazorWebViewIsVisible = blazorWebView.IsVisible;
            _savedBlazorWebViewOpacity = blazorWebView.Opacity;
            _savedBlazorWebViewInputTransparent = blazorWebView.InputTransparent;
            _nativeVideoWebViewShellSaved = true;
        }

        blazorWebView.Opacity = 0;
        blazorWebView.InputTransparent = true;
        blazorWebView.IsVisible = false;
        MauiNativeVideoChrome.SetBackgroundUiPaused(true);
        // HTML5 ambient audio survives WebView.OnPause. Halt it before timers freeze.
        _ = TryEvaluateWebViewJs(
            "try{if(window.K7&&K7.AmbientTheme&&K7.AmbientTheme.stop)K7.AmbientTheme.stop();}catch(e){}");
#if ANDROID
        Platforms.Android.AndroidOverlayComposition.SetDraws(blazorWebView, draws: false);
        SuppressWebViewFocusForNativeChrome();
        TryPauseAndroidWebView();
#endif
    }

#if WINDOWS
    /// <summary>
    /// HLS / Video.js: WebView paints frames under native XAML chrome; input stays on the overlay.
    /// </summary>
    private void ShowBlazorWebViewUnderNativeChrome()
    {
        if (!_nativeVideoWebViewShellSaved)
        {
            _savedBlazorWebViewIsVisible = blazorWebView.IsVisible;
            _savedBlazorWebViewOpacity = blazorWebView.Opacity;
            _savedBlazorWebViewInputTransparent = blazorWebView.InputTransparent;
            _nativeVideoWebViewShellSaved = true;
        }

        blazorWebView.IsVisible = true;
        blazorWebView.Opacity = 1;
        blazorWebView.InputTransparent = true;
        _ = TryEvaluateWebViewJs(
            "try{if(window.K7&&K7.AmbientTheme&&K7.AmbientTheme.stop)K7.AmbientTheme.stop();"
            + "if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(true,true);"
            + "if(window.K7&&K7.setNativePlayerPlaying)K7.setNativePlayerPlaying(false);}catch(e){}");
    }

    /// <summary>
    /// Remote-control UI lives in Blazor; drop InputTransparent and show the WebView fully.
    /// </summary>
    private void RevealBlazorWebViewForRemoteControl()
    {
        _nativeVideoWebViewShellSaved = false;
        blazorWebView.IsVisible = true;
        blazorWebView.Opacity = 1;
        blazorWebView.InputTransparent = false;
        _ = TryEvaluateWebViewJs(
            "try{if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(false,false);"
            + "if(window.K7&&K7.setNativePlayerPlaying)K7.setNativePlayerPlaying(false);}catch(e){}");
    }
#else
    private void RevealBlazorWebViewForRemoteControl()
    {
        _nativeVideoWebViewShellSaved = false;
        blazorWebView.IsVisible = true;
        blazorWebView.Opacity = 1;
        blazorWebView.InputTransparent = false;
#if ANDROID
        Platforms.Android.AndroidOverlayComposition.Reset(blazorWebView);
#endif
    }
#endif

    private void RestoreBlazorWebViewAfterNativeVideo()
    {
        if (!_nativeVideoWebViewShellSaved)
            return;

        MauiNativeVideoChrome.SetBackgroundUiPaused(false);
#if ANDROID
        Platforms.Android.AndroidOverlayComposition.Reset(blazorWebView);
        TryResumeAndroidWebView();
#endif
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
    private void TryPauseAndroidWebView()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is global::Android.Webkit.WebView webView)
                webView.OnPause();
        }
        catch
        {
        }
    }

    private void TryResumeAndroidWebView()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is global::Android.Webkit.WebView webView)
                webView.OnResume();
        }
        catch
        {
        }
    }

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
        if (!WantsNativeVideoChrome || _nativeOverlay is null)
            return false;

        return _nativeOverlay.HandleBack();
    }

    internal bool TryHandleNativeVideoKey(string key, bool isKeyUp = false)
    {
        if (!WantsNativeVideoChrome || _nativeOverlay is null)
            return false;

        return _nativeOverlay.HandleKey(key, isKeyUp);
    }
}
