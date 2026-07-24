#if WINDOWS
using K7.Clients.Shared.Helpers;
using Microsoft.JSInterop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using VirtualKey = Windows.System.VirtualKey;
using WinUiWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private bool _windowsEscapeHandlerAttached;
    private bool _windowsWebViewEscapeAttached;
    private bool _windowsWebViewCoreInitHooked;
    private bool _windowsWebViewPointerHooked;
    private WinUiWebView2? _windowsWebViewEscapeTarget;
    private List<(IView View, int Index)>? _windowsVideoLayoutHiddenViews;

    partial void InitializePlayerPlatform()
    {
        // Music uses WebView2 audioplayer.js (WindowsAudioPlayback). Keep native MediaElements idle.
        DisableNativeAudioElements();
    }

    partial void DetachPlayerPlatform()
    {
        DetachWindowsEscapeHandler();
        DetachWindowsWebViewEscapeHandler();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EnsureWindowsEscapeHandler();
        EnsureWindowsWebViewInputHooks();
    }

    partial void ConfigureWindowsVideoPlayerLayout()
    {
        SyncWindowsStreamAuthContext();
        DisableNativeAudioElements();

        NativePlayer.IsVisible = false;
        NativePlayer.InputTransparent = true;
        NativePlayer.IsEnabled = false;
        NativePlayer.ShouldShowPlaybackControls = false;
        NativePlayerCloseButton.IsVisible = false;

        if (_playerService.IsVisible)
        {
            NativePlayer.Stop();
            NativePlayer.Source = null;

            HideNonWebViewSiblingsForVideoSession();

            BackgroundColor = Colors.Black;
            Padding = new Microsoft.Maui.Thickness(0);
            blazorWebView.ZIndex = 10;
            blazorWebView.InputTransparent = false;
            blazorWebView.IsEnabled = true;
            blazorWebView.HorizontalOptions = LayoutOptions.Fill;
            blazorWebView.VerticalOptions = LayoutOptions.Fill;
            blazorWebView.Focus();
            EnsureWindowsEscapeHandler();
            EnsureWindowsWebViewInputHooks();
            ApplyWindowsWebViewOpaqueInputSurface();
            FocusWindowsWebViewAsync().FireAndForget();
            FocusVideoOverlayAsync().FireAndForget();
        }
        else
        {
            RestoreWindowsVideoLayoutHiddenViews();

            NativePlayer.IsEnabled = true;
            NativePlayer.Stop();
            NativePlayer.Source = null;
            BackgroundColor = Colors.Transparent;
            blazorWebView.ZIndex = 2;
        }
    }

    private static void DisableNativeAudioElements()
    {
        // Elements remain in XAML for other TFMs; Windows never drives them.
    }

    private void HideNonWebViewSiblingsForVideoSession()
    {
        _windowsVideoLayoutHiddenViews ??= [];
        _windowsVideoLayoutHiddenViews.Clear();

        for (var i = RootGrid.Children.Count - 1; i >= 0; i--)
        {
            var child = RootGrid[i];
            if (ReferenceEquals(child, blazorWebView))
                continue;

            // Splash is dismissed permanently; never restore it after video ends.
            if (!ReferenceEquals(child, SplashOverlay))
                _windowsVideoLayoutHiddenViews.Add((child, i));

            RootGrid.Children.RemoveAt(i);
        }
    }

    private void RestoreWindowsVideoLayoutHiddenViews()
    {
        if (_windowsVideoLayoutHiddenViews is null || _windowsVideoLayoutHiddenViews.Count == 0)
            return;

        foreach (var (view, index) in _windowsVideoLayoutHiddenViews.OrderBy(static entry => entry.Index))
        {
            if (RootGrid.Children.Contains(view))
                continue;

            var insertIndex = Math.Min(index, RootGrid.Children.Count);
            RootGrid.Insert(insertIndex, view);
        }

        _windowsVideoLayoutHiddenViews.Clear();
    }

    private void ApplyWindowsWebViewOpaqueInputSurface()
    {
        if (!TryGetWindowsWebView(out var webView))
            return;

        webView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 13, 9, 7);
        webView.IsHitTestVisible = true;
        webView.IsTabStop = true;
    }

    private async Task FocusWindowsWebViewAsync()
    {
        await Task.Delay(100);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (TryGetWindowsWebView(out var webView))
            {
                webView.IsHitTestVisible = true;
                webView.IsTabStop = true;
                webView.Focus(FocusState.Programmatic);
            }
        });
    }

    private void EnsureWindowsWebViewInputHooks()
    {
        if (!TryGetWindowsWebView(out var webView))
            return;

        webView.IsHitTestVisible = true;
        webView.IsTabStop = true;

        if (!_windowsWebViewPointerHooked)
        {
            webView.PointerPressed += OnWindowsWebViewPointerPressed;
            _windowsWebViewPointerHooked = true;
        }

        if (!_windowsWebViewCoreInitHooked)
        {
            webView.CoreWebView2Initialized += OnWindowsWebViewCoreInitialized;
            _windowsWebViewCoreInitHooked = true;
        }

        AttachWindowsWebViewEscapeHandler(webView);
    }

    private void OnWindowsWebViewCoreInitialized(WinUiWebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null)
            return;

        sender.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 13, 9, 7);
        sender.IsHitTestVisible = true;
        AttachWindowsWebViewEscapeHandler(sender);
        ApplyWindowsWebViewOpaqueInputSurface();
    }

    private void OnWindowsWebViewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not WinUiWebView2 webView)
            return;

        webView.Focus(FocusState.Pointer);
    }

    private void AttachWindowsWebViewEscapeHandler(WinUiWebView2 webView)
    {
        if (_windowsWebViewEscapeAttached)
            return;

        webView.KeyDown += OnWindowsWebViewKeyDown;
        _windowsWebViewEscapeTarget = webView;
        _windowsWebViewEscapeAttached = true;
    }

    private void DetachWindowsWebViewEscapeHandler()
    {
        if (!_windowsWebViewEscapeAttached)
            return;

        if (_windowsWebViewEscapeTarget is not null)
            _windowsWebViewEscapeTarget.KeyDown -= OnWindowsWebViewKeyDown;

        _windowsWebViewEscapeTarget = null;
        _windowsWebViewEscapeAttached = false;
    }

    private void OnWindowsWebViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_playerService.IsVisible)
            return;

        if (e.Key is not VirtualKey.Escape)
            return;

        e.Handled = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_playerService is Services.PlayerService playerService)
                playerService.OnBackPressed();
            else
                DispatchBackAsEscape();
        });
    }

    private bool TryGetWindowsWebView(out WinUiWebView2 webView)
    {
        if (blazorWebView.Handler?.PlatformView is WinUiWebView2 platformView)
        {
            webView = platformView;
            return true;
        }

        webView = null!;
        return false;
    }

    private async Task FocusVideoOverlayAsync()
    {
        await Task.Delay(150);

        _ = blazorWebView.TryDispatchAsync(async sp =>
        {
            try
            {
                var js = sp.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("SpatialNav.focusFirst", ".video-controls-overlay");
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
        });
    }

    private void EnsureWindowsEscapeHandler()
    {
        if (_windowsEscapeHandlerAttached)
            return;

        if (Handler?.PlatformView is not UIElement root)
            return;

        root.KeyDown += OnWindowsRootKeyDown;
        _windowsEscapeHandlerAttached = true;
    }

    private void DetachWindowsEscapeHandler()
    {
        if (!_windowsEscapeHandlerAttached)
            return;

        if (Handler?.PlatformView is UIElement root)
            root.KeyDown -= OnWindowsRootKeyDown;

        _windowsEscapeHandlerAttached = false;
    }

    private void OnWindowsRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_playerService.IsVisible)
            return;

        if (e.Key is not VirtualKey.Escape)
            return;

        e.Handled = true;

        if (_playerService is Services.PlayerService playerService)
            playerService.OnBackPressed();
        else
            DispatchBackAsEscape();
    }
}
#endif
