#if WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using K7.Clients.MAUI.Constants;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using Microsoft.JSInterop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using SkiaSharp;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.System;
using VirtualKey = Windows.System.VirtualKey;
using WinUiWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private SystemMediaTransportControls? _smtc;
    private bool _windowsEscapeHandlerAttached;
    private bool _windowsWebViewEscapeAttached;
    private bool _windowsWebViewCoreInitHooked;
    private bool _windowsWebViewPointerHooked;
    private WinUiWebView2? _windowsWebViewEscapeTarget;
    private List<(IView View, int Index)>? _windowsVideoLayoutHiddenViews;

    partial void InitializePlayerPlatform()
    {
        _audioPlayerService.CurrentTrackChanged += OnAudioTrackChangedWindows;
        _audioPlayerService.PlaybackStateChanged += OnAudioPlaybackStateChangedWindows;
        NativeAudioPlayer.HandlerChanged += OnNativeAudioPlayerHandlerChangedWindows;
        NativeAudioPlayer.MediaOpened += OnNativeAudioPlayerMediaOpenedWindows;
    }

    partial void DetachPlayerPlatform()
    {
        _audioPlayerService.CurrentTrackChanged -= OnAudioTrackChangedWindows;
        _audioPlayerService.PlaybackStateChanged -= OnAudioPlaybackStateChangedWindows;
        NativeAudioPlayer.HandlerChanged -= OnNativeAudioPlayerHandlerChangedWindows;
        NativeAudioPlayer.MediaOpened -= OnNativeAudioPlayerMediaOpenedWindows;

        if (_smtc is not null)
        {
            _smtc.ButtonPressed -= OnSmtcButtonPressed;
            _smtc = null;
        }

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

        NativePlayer.IsVisible = false;
        NativePlayer.InputTransparent = true;
        NativePlayer.IsEnabled = false;
        NativePlayer.ShouldShowPlaybackControls = false;
        NativePlayerCloseButton.IsVisible = false;
        NativeAudioPlayer.InputTransparent = true;
        NativeAudioPlayer.IsEnabled = false;

        if (_playerService.IsVisible)
        {
            NativePlayer.Stop();
            NativePlayer.Source = null;
            NativeAudioPlayer.Stop();

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
            NativeAudioPlayer.IsEnabled = true;
            NativeAudioPlayer.InputTransparent = true;
            NativePlayer.Stop();
            NativePlayer.Source = null;
            BackgroundColor = Colors.Transparent;
            blazorWebView.ZIndex = 2;
        }
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

        System.Diagnostics.Debug.WriteLine(
            $"[K7-Player] Windows layout: BlazorWebView only ({_windowsVideoLayoutHiddenViews.Count} siblings removed)");
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

        System.Diagnostics.Debug.WriteLine(
            $"[K7-Player] Windows layout restored ({_windowsVideoLayoutHiddenViews.Count} siblings)");
        _windowsVideoLayoutHiddenViews.Clear();
    }

    private void ApplyWindowsWebViewOpaqueInputSurface()
    {
        if (!TryGetWindowsWebView(out var webView))
        {
            System.Diagnostics.Debug.WriteLine("[K7-Player] WinUI WebView2 not ready for opaque input surface");
            return;
        }

        webView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 13, 9, 7);
        webView.IsHitTestVisible = true;
        webView.IsTabStop = true;
        System.Diagnostics.Debug.WriteLine("[K7-Player] Windows layout configured; opaque WebView2 input surface applied");
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
                System.Diagnostics.Debug.WriteLine("[K7-Player] WinUI WebView2 focused for input");
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
        {
            System.Diagnostics.Debug.WriteLine("[K7-Player] CoreWebView2 init failed: " + args.Exception.Message);
            return;
        }

        sender.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 13, 9, 7);
        sender.IsHitTestVisible = true;
        AttachWindowsWebViewEscapeHandler(sender);
        ApplyWindowsWebViewOpaqueInputSurface();
        System.Diagnostics.Debug.WriteLine("[K7-Player] CoreWebView2 ready; opaque background applied");
    }

    private void OnWindowsWebViewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not WinUiWebView2 webView)
            return;

        webView.Focus(FocusState.Pointer);
        System.Diagnostics.Debug.WriteLine("[K7-Player] WebView2 PointerPressed -> focus");
    }

    private void AttachWindowsWebViewEscapeHandler(WinUiWebView2 webView)
    {
        if (_windowsWebViewEscapeAttached)
            return;

        webView.KeyDown += OnWindowsWebViewKeyDown;
        _windowsWebViewEscapeTarget = webView;
        _windowsWebViewEscapeAttached = true;
        System.Diagnostics.Debug.WriteLine("[K7-Player] WebView2 KeyDown wired");
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

        System.Diagnostics.Debug.WriteLine("[K7-Player] WebView2 KeyDown Escape");
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

        System.Diagnostics.Debug.WriteLine("[K7-Player] WinUI root KeyDown Escape");
        e.Handled = true;

        if (_playerService is Services.PlayerService playerService)
            playerService.OnBackPressed();
        else
            DispatchBackAsEscape();
    }

    private void OnNativeAudioPlayerHandlerChangedWindows(object? sender, EventArgs e)
        => OnNativeAudioPlayerHandlerChangedWindowsAsync().FireAndForget();

    private async Task OnNativeAudioPlayerHandlerChangedWindowsAsync()
    {
        if (!TrySetupSmtc())
        {
            await Task.Delay(MauiTimeouts.WindowsBlazorInitDelay);
            TrySetupSmtc();
        }
    }

    private void OnNativeAudioPlayerMediaOpenedWindows(object? sender, EventArgs e)
    {
        TrySetupSmtc();
        OnAudioTrackChangedWindows(_audioPlayerService.CurrentTrack);
    }

    private bool TrySetupSmtc()
    {
        if (_smtc is not null) return true;

        var platformView = NativeAudioPlayer.Handler?.PlatformView;
        if (platformView is not Panel panel)
        {
            System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC: platform view is {platformView?.GetType().Name ?? "null"}, expected Panel");
            return false;
        }

        MediaPlayerElement? mpe = null;
        foreach (var child in panel.Children)
        {
            if (child is MediaPlayerElement found)
            {
                mpe = found;
                break;
            }
        }

        if (mpe?.MediaPlayer is null)
        {
            System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC: no MediaPlayerElement in panel ({panel.Children.Count} children)");
            return false;
        }

        mpe.MediaPlayer.CommandManager.IsEnabled = false;

        var smtc = mpe.MediaPlayer.SystemMediaTransportControls;
        smtc.IsEnabled = true;
        smtc.IsPlayEnabled = true;
        smtc.IsPauseEnabled = true;
        smtc.IsNextEnabled = true;
        smtc.IsPreviousEnabled = true;
        smtc.ButtonPressed += OnSmtcButtonPressed;
        _smtc = smtc;

        System.Diagnostics.Trace.WriteLine("[K7-Audio] SMTC setup OK");
        return true;
    }

    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _audioPlayerService.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _audioPlayerService.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _ = _audioPlayerService.NextAsync();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _ = _audioPlayerService.PreviousAsync();
                    break;
            }
        });
    }

    private void OnAudioPlaybackStateChangedWindows(Server.Domain.Enums.PlaybackState state)
    {
        if (_smtc is null) return;

        _smtc.PlaybackStatus = state switch
        {
            Server.Domain.Enums.PlaybackState.Playing => MediaPlaybackStatus.Playing,
            Server.Domain.Enums.PlaybackState.Paused => MediaPlaybackStatus.Paused,
            Server.Domain.Enums.PlaybackState.Buffering => MediaPlaybackStatus.Changing,
            _ => MediaPlaybackStatus.Stopped,
        };
    }

    private void OnAudioTrackChangedWindows(AudioQueueItem? track)
    {
        if (track is null) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            TrySetupSmtc();
            if (_smtc is null) return;

            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = track.Title ?? "";
                updater.MusicProperties.Artist = track.Artist ?? "";
                updater.MusicProperties.AlbumTitle = track.AlbumTitle ?? "";

                if (!string.IsNullOrEmpty(track.CoverUrl))
                {
                    var absoluteUri = _k7ServerService.GetAbsoluteUri(track.CoverUrl);
                    if (absoluteUri is not null)
                    {
                        var thumbnail = await FetchThumbnailAsync(absoluteUri);
                        if (thumbnail is not null)
                            updater.Thumbnail = thumbnail;
                    }
                }

                updater.Update();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC update failed: {ex.Message}");
            }
        });
    }

    private async Task<RandomAccessStreamReference?> FetchThumbnailAsync(Uri uri)
    {
        try
        {
            var bytes = await _k7ServerService.HttpClient.GetByteArrayAsync(uri);

            // SMTC does not support WebP - convert to JPEG via SkiaSharp
            using var original = SKBitmap.Decode(bytes);
            if (original is null) return null;

            using var image = SKImage.FromBitmap(original);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            var jpegBytes = encoded.ToArray();

            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(jpegBytes.AsBuffer());
            stream.Seek(0);
            return RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[K7-Audio] Thumbnail fetch failed: {ex.Message}");
            return null;
        }
    }
}
#endif
