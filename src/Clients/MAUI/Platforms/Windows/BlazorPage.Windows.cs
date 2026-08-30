#if WINDOWS
using K7.Clients.MAUI.Playback;
using K7.Clients.MAUI.Platforms.Windows;
using K7.Clients.MAUI.Platforms.Windows.Services;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using VirtualKey = Windows.System.VirtualKey;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private bool _windowsEscapeHandlerAttached;
    private bool _windowsCloseHandlerAttached;
    private KeyEventHandler? _windowsPreviewKeyDownHandler;
    private KeyEventHandler? _windowsPreviewKeyUpHandler;
    private TypedEventHandler<object, WindowEventArgs>? _windowsClosedHandler;
    private TypedEventHandler<AppWindow, AppWindowClosingEventArgs>? _windowsClosingHandler;
    private AppWindow? _windowsAppWindow;
    private WindowsVlcVideoPlayer? _vlcPlayer;
    private bool _vlcEventsHooked;
    private string? _directTrackOverrideUrl;

    partial void InitializePlayerPlatform()
    {
        DisableNativeAudioElements();
        _playerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
        _playerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
        _playerService.EnterFullScreenRequested += OnWindowsEnterFullScreen;
        _playerService.ExitFullScreenRequested += OnWindowsExitFullScreen;
        // Attach Closing before first play so exit during Direct Play always tears down LibVLC.
        EnsureWindowsCloseHandler();
    }

    partial void DetachPlayerPlatform()
    {
        _playerService.EnterFullScreenRequested -= OnWindowsEnterFullScreen;
        _playerService.ExitFullScreenRequested -= OnWindowsExitFullScreen;
        _directTrackOverrideUrl = null;
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch
        {
            StopWindowsVlc();
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
        DetachWindowsEscapeHandler();
        DetachWindowsCloseHandler();
    }

    private void DisposeWindowsVlcPlayer()
    {
        try
        {
            _vlcPlayer?.Dispose();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc dispose on close " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EnsureWindowsEscapeHandler();
        // Window.Handler may be null during InitializePlayerPlatform - retry Closing attach here.
        EnsureWindowsCloseHandler();
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
            try
            {
                NativePlayer.Stop();
                NativePlayer.Source = null;
            }
            catch
            {
            }

            BackgroundColor = Colors.Black;
            Padding = new Microsoft.Maui.Thickness(0);
            EnsureWindowsEscapeHandler();
            EnsureWindowsCloseHandler();
        }
        else
        {
            NativePlayer.IsEnabled = true;
            try
            {
                NativePlayer.Stop();
                NativePlayer.Source = null;
            }
            catch
            {
            }

            BackgroundColor = Colors.Transparent;
            StopWindowsVlc();
        }
    }

    private static void DisableNativeAudioElements()
    {
    }

    internal bool IsWindowsVlcActive => _vlcPlayer?.IsActive == true;

    internal void SetWindowsVlcSurfaceVisible(bool visible) =>
        _vlcPlayer?.SetSurfaceVisible(visible);

    internal static bool ShouldUseWindowsVlc(PlayerSource? source) =>
        source is not null
        && WindowsVideoPlayback.ShouldUseLibVlc(source.MimeType, source.Url);

    internal bool TryOpenWindowsVlc(PlayerSource source)
    {
        if (!ShouldUseWindowsVlc(source) || string.IsNullOrEmpty(source.Url))
            return false;

        EnsureWindowsCloseHandler();
        NativePlayer.IsVisible = false;
        try
        {
            NativePlayer.Stop();
            NativePlayer.Source = null;
        }
        catch
        {
        }

        _vlcPlayer ??= new WindowsVlcVideoPlayer(RootGrid);
        HookWindowsVlcOnce();
        VlcSubtitleStyle.SetSettings(
            _playerService.VideoPlayerUxSettings ?? VlcSubtitleStyle.GetSettings());

        var startSeconds = source.PendingSeekTime is double pending && pending > 1
            ? pending
            : 0;
        _vlcPlayer.Play(
            source.Url,
            ResolveNativePlayerAuthorizationHeader(),
            startSeconds,
            ResolveVlcAudioOrdinal(),
            ResolveVlcSubtitleOrdinal(),
            hlsAudioTrackIndex: null,
            _playerService.Duration);
        if (_playerService.SelectedSubtitleTrack is { IsTextBased: true })
            _vlcPlayer.SetOverlayOwnsTextSubs(true);
        // Reset any leftover mixer attenuation from older builds; volume is software-only (0-200).
        WindowsAppAudioVolume.TrySet(1.0);
        _vlcPlayer.SetVolume(_playerService.Volume);
        _vlcPlayer.SetMuted(_playerService.IsMuted);
        _vlcPlayer.SetRate(_playerService.PlaybackRate > 0 ? _playerService.PlaybackRate : 1);
        _vlcPlayer.ApplyAspect(_playerService.AspectRatio);

        var kind = LocalPlaybackUrl.IsLocalFile(source.Url)
            ? "file"
            : StreamingSourceKind.IsHls(source.MimeType, source.Url)
                ? "hls"
                : "direct";
        if (startSeconds > 1 && kind is not "hls")
            _playerService.CurrentTime = startSeconds;

        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Buffering;
        VlcPlayerLog.Info(
            "bind kind="
            + kind
            + " pipeline=vlc url="
            + VlcPlayerLog.SummarizeUrl(source.Url)
            + " mime="
            + (source.MimeType ?? "-")
            + " quality="
            + (_playerService.SelectedQuality?.Label ?? "-")
            + " start="
            + startSeconds.ToString("F1")
            + "s");
        return true;
    }

    /// <summary>
    /// Stops and fully disposes LibVLC so Direct and Video.js never run in parallel.
    /// Next Direct Play recreates a fresh <see cref="WindowsVlcVideoPlayer"/>.
    /// </summary>
    internal void StopWindowsVlc()
    {
        var player = _vlcPlayer;
        if (player is null)
            return;

        // Drop the field first so overlay/control handlers cannot touch a half-disposed engine
        // during a fast Direct -> HLS quality swap.
        _vlcPlayer = null;
        _vlcEventsHooked = false;

        try
        {
            player.Stop();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc pipeline stop " + ex.GetType().Name);
        }

        try
        {
            player.Dispose();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc pipeline dispose " + ex.GetType().Name);
        }

        // Reset leftover mixer gain so a later Direct session starts at unity (software gain only).
        WindowsAppAudioVolume.TrySet(1.0);
    }

    internal bool IsWindowsWebVideoActive =>
        _playerService.Source is not null
        && WindowsVideoPlayback.ShouldUseWebVideoPlayer(
            _playerService.Source.MimeType,
            _playerService.Source.Url);

    internal bool TryHandleWindowsVlcPlay()
    {
        if (!IsWindowsVlcActive)
            return false;

        _vlcPlayer!.Resume();
        return true;
    }

    internal bool TryHandleWindowsVlcPause()
    {
        if (!IsWindowsVlcActive)
            return false;

        _vlcPlayer!.Pause();
        return true;
    }

    internal bool TryHandleWindowsVlcStop()
    {
        if (!IsWindowsVlcActive)
            return false;

        StopWindowsVlc();
        return true;
    }

    internal bool TryHandleWindowsVlcMute(bool muted)
    {
        if (!IsWindowsVlcActive)
            return false;

        _vlcPlayer!.SetMuted(muted);
        return true;
    }

    internal bool TryHandleWindowsVlcVolume(double volume)
    {
        if (!IsWindowsVlcActive)
            return false;

        // IPlayerService volume -> LibVLC software gain only (parity with Video.js element volume).
        _vlcPlayer!.SetVolume(volume);
        return true;
    }

    internal bool TryHandleWindowsVlcRate(double rate)
    {
        if (!IsWindowsVlcActive)
            return false;

        _vlcPlayer!.SetRate(rate);
        return true;
    }

    internal bool TryHandleWindowsVlcAspect(AspectRatioMode mode)
    {
        if (!IsWindowsVlcActive)
            return false;

        _vlcPlayer!.ApplyAspect(mode);
        return true;
    }

    internal void UpdateWindowsVlcAuthorization()
    {
        _vlcPlayer?.UpdateAuthorization(ResolveNativePlayerAuthorizationHeader());
    }

    internal void ApplyPendingWindowsSubtitleStyle()
    {
        if (IsWindowsVlcActive)
            _vlcPlayer?.RefreshSubtitleStyle();
    }

    internal void ReleaseSidecarTextSubtitles() =>
        _vlcPlayer?.SetOverlayOwnsTextSubs(false);

    internal void NotifySidecarTextSubtitles(bool ready)
    {
        if (!IsWindowsVlcActive)
            return;

        _vlcPlayer!.SetOverlayOwnsTextSubs(ready);
    }

    internal bool TryGetWindowsVlcMediaSeconds(out double seconds)
    {
        seconds = 0;
        if (!IsWindowsVlcActive)
            return false;

        seconds = _vlcPlayer!.PositionSeconds;
        return true;
    }

    private double GetWindowsVlcPositionSeconds() =>
        TryGetWindowsVlcMediaSeconds(out var seconds) ? seconds : 0;

    private Task SeekWindowsVideoAsync(double positionSeconds) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!IsWindowsVlcActive)
                return;

            var resumePlayback = _playerService.PlaybackState
                is Server.Domain.Enums.PlaybackState.Playing
                or Server.Domain.Enums.PlaybackState.Buffering;

            var targetSeconds = Math.Max(0, positionSeconds);
            // Prefer metadata duration: VLC Length can be 0/short around reopen.
            var knownDuration = Math.Max(_playerService.Duration, _vlcPlayer!.DurationSeconds);
            if (knownDuration > 1)
            {
                _vlcPlayer.PinDuration(knownDuration);
                targetSeconds = Math.Min(targetSeconds, knownDuration);
            }

            _vlcPlayer.Seek(targetSeconds);
            _playerService.CurrentTime = targetSeconds;
            if (knownDuration > 1 && _playerService.Duration <= 1)
                _playerService.Duration = knownDuration;
            if (resumePlayback)
                _vlcPlayer.Resume();
        });

    private void HookWindowsVlcOnce()
    {
        if (_vlcPlayer is null || _vlcEventsHooked)
            return;

        _vlcEventsHooked = true;
        _vlcPlayer.Playing += OnWindowsVlcPlaying;
        _vlcPlayer.Paused += OnWindowsVlcPaused;
        _vlcPlayer.Ended += OnWindowsVlcEnded;
        _vlcPlayer.EncounteredError += OnWindowsVlcError;
        _vlcPlayer.PositionChanged += OnWindowsVlcPosition;
        _vlcPlayer.DurationChanged += OnWindowsVlcDuration;
        _vlcPlayer.FirstFrame += OnWindowsVlcFirstFrame;
        _vlcPlayer.Reopening += OnWindowsVlcReopening;
    }

    private void OnWindowsVlcPlaying()
    {
        if (!IsWindowsVlcActive)
            return;

        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Playing;
        if (_playerService.Source is { PendingSeekTime: double pending } source
            && pending > 1
            && Math.Abs(_vlcPlayer!.PositionSeconds - pending) <= 30)
        {
            source.PendingSeekTime = null;
        }
    }

    private void OnWindowsVlcPaused()
    {
        if (IsWindowsVlcActive)
            _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Paused;
    }

    private void OnWindowsVlcEnded()
    {
        if (IsWindowsVlcActive)
            _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Ended;
    }

    private void OnWindowsVlcError(string detail)
    {
        if (!IsWindowsVlcActive)
            return;

        VlcPlayerLog.Warn("vlc playback failed " + VlcPlayerLog.SummarizeUrl(detail));
        if (detail.Contains("401", StringComparison.Ordinal)
            || detail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            _ = TryRecoverNativeVideoAuthAsync("vlc " + detail);
            return;
        }

        ReportNativePlayerMediaFailedToServer("vlc " + detail);
    }

    private void OnWindowsVlcPosition(double seconds)
    {
        if (!IsWindowsVlcActive || seconds < 0)
            return;

        _playerService.CurrentTime = seconds;
    }

    private void OnWindowsVlcDuration(double seconds)
    {
        if (!IsWindowsVlcActive || seconds <= 0)
            return;

        // Once metadata duration is known, never replace it with VLC Length.
        var known = _playerService.Duration;
        if (known > 1)
        {
            if (Math.Abs(seconds - known) > 0.5 && seconds >= known * 0.9 && seconds <= known * 1.1)
                _playerService.Duration = Math.Max(known, seconds);
            return;
        }

        _playerService.Duration = seconds;
    }

    private void OnWindowsVlcReopening()
    {
        // Seek/audio reopen must not flash a zero duration on the seekbar.
        if (_playerService.Duration > 1)
            _vlcPlayer?.PinDuration(_playerService.Duration);
        _nativeOverlay?.ShowTransientVeil();
    }

    private void OnWindowsVlcFirstFrame()
    {
        _nativeOverlay?.NotifyFirstFrameReady();
        var url = _playerService.Source?.Url;
        if (_directTrackOverrideUrl == url)
            return;

        _directTrackOverrideUrl = url;
        try
        {
            _vlcPlayer?.LogEsTracks();
        }
        catch (InvalidOperationException)
        {
            // LibVLCSharp can expose null MediaTrack entries before ES are ready.
        }
    }

    private void OnSwitchAudioTrack(string trackName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsWindowsVlcActive)
                TrySwitchVlcAudioTrack(trackName, attempt: 0);
        });
    }

    private void OnSwitchSubtitleTrack(string? slug)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsWindowsVlcActive)
                TrySwitchVlcSubtitleTrack(slug, attempt: 0);
        });
    }

    private int? ResolveVlcAudioOrdinal()
    {
        if (_playerService.SelectedAudioTrack is not { } audio)
            return null;

        var ordered = _playerService.AudioTracks.OrderBy(t => t.Index).ToList();
        var index = ordered.FindIndex(t => t.Index == audio.Index);
        return index >= 0 ? index : null;
    }

    private int? ResolveVlcSubtitleOrdinal()
    {
        if (_playerService.SelectedSubtitleTrack is not { } sub)
            return null;

        var ordered = _playerService.SubtitleTracks.OrderBy(t => t.Index).ToList();
        var index = ordered.FindIndex(t => t.Index == sub.Index);
        return index >= 0 ? index : null;
    }

    private void TrySwitchVlcAudioTrack(string trackName, int attempt)
    {
        if (!IsWindowsVlcActive)
            return;

        var ordinal = 0;
        AudioFileTrackDto? catalog = null;
        if (trackName.StartsWith("audio-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trackName.AsSpan(6), out var fileStreamIndex))
        {
            var ordered = _playerService.AudioTracks.OrderBy(t => t.Index).ToList();
            var index = ordered.FindIndex(t => t.Index == fileStreamIndex);
            if (index >= 0)
            {
                ordinal = index;
                catalog = ordered[index];
            }
        }

        if (_vlcPlayer!.TrySelectAudio(ordinal, catalog?.Language, catalog?.Name))
            return;

        if (attempt < 5)
            ScheduleVlcTrackRetry(() => TrySwitchVlcAudioTrack(trackName, attempt + 1));
    }

    private void TrySwitchVlcSubtitleTrack(string? slug, int attempt)
    {
        if (!IsWindowsVlcActive)
            return;

        if (slug is null)
        {
            _vlcPlayer!.TrySelectSubtitle(null, null, null);
            return;
        }

        // Text SRT/VTT: XAML sidecar owns paint + live style. Do not select VLC SPU.
        if (_playerService.SelectedSubtitleTrack is { IsTextBased: true })
        {
            _vlcPlayer!.TrySelectSubtitle(null, null, null);
            _vlcPlayer.SetOverlayOwnsTextSubs(true);
            return;
        }

        int? ordinal = null;
        string? language = null;
        string? name = null;
        if (int.TryParse(slug.AsSpan(4), out var fileStreamIndex))
        {
            var subtitleTracks = _playerService.SubtitleTracks.OrderBy(t => t.Index).ToList();
            for (var idx = 0; idx < subtitleTracks.Count; idx++)
            {
                if (subtitleTracks[idx].Index == fileStreamIndex)
                {
                    ordinal = idx;
                    language = subtitleTracks[idx].Language;
                    name = subtitleTracks[idx].Name;
                    break;
                }
            }
        }

        if (_vlcPlayer!.TrySelectSubtitle(ordinal, language, name))
            return;

        if (attempt < 5)
            ScheduleVlcTrackRetry(() => TrySwitchVlcSubtitleTrack(slug, attempt + 1));
    }

    private static void ScheduleVlcTrackRetry(Action retry)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            MainThread.BeginInvokeOnMainThread(retry);
        });
    }

    private void EnsureWindowsEscapeHandler()
    {
        EnsureWindowsCloseHandler();
        if (_windowsEscapeHandlerAttached)
            return;

        if (!TryGetWindowsContent(out var content))
            return;

        _windowsPreviewKeyDownHandler ??= OnWindowsPreviewKeyDown;
        _windowsPreviewKeyUpHandler ??= OnWindowsPreviewKeyUp;
        content.AddHandler(UIElement.PreviewKeyDownEvent, _windowsPreviewKeyDownHandler, handledEventsToo: true);
        content.AddHandler(UIElement.PreviewKeyUpEvent, _windowsPreviewKeyUpHandler, handledEventsToo: true);
        _windowsEscapeHandlerAttached = true;
    }

    private void EnsureWindowsCloseHandler()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
            return;

        _windowsAppWindow ??= native.AppWindow;
        if (_windowsAppWindow is not null && _windowsClosingHandler is null)
        {
            _windowsClosingHandler = OnWindowsAppWindowClosing;
            _windowsAppWindow.Closing += _windowsClosingHandler;
        }

        if (_windowsClosedHandler is null)
        {
            _windowsClosedHandler = OnWindowsNativeWindowClosed;
            native.Closed += _windowsClosedHandler;
        }

        _windowsCloseHandlerAttached = _windowsClosingHandler is not null || _windowsClosedHandler is not null;
    }

    private void DetachWindowsCloseHandler()
    {
        if (!_windowsCloseHandlerAttached)
            return;

        if (_windowsAppWindow is not null && _windowsClosingHandler is not null)
            _windowsAppWindow.Closing -= _windowsClosingHandler;

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
            && _windowsClosedHandler is not null)
        {
            native.Closed -= _windowsClosedHandler;
        }

        _windowsAppWindow = null;
        _windowsCloseHandlerAttached = false;
    }

    private void OnWindowsAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Before WinUI tears down SwapChainPanel: drop D3D callbacks (no COM Dispose) and
        // background LibVLC Stop so Present cannot race window destruction.
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc prepare exit " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    private void OnWindowsNativeWindowClosed(object sender, WindowEventArgs args)
    {
        // Safety net if Closing did not run (or HLS-only session).
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc closed exit " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    private void DetachWindowsEscapeHandler()
    {
        if (!_windowsEscapeHandlerAttached)
            return;

        if (TryGetWindowsContent(out var content))
        {
            if (_windowsPreviewKeyDownHandler is not null)
                content.RemoveHandler(UIElement.PreviewKeyDownEvent, _windowsPreviewKeyDownHandler);
            if (_windowsPreviewKeyUpHandler is not null)
                content.RemoveHandler(UIElement.PreviewKeyUpEvent, _windowsPreviewKeyUpHandler);
        }

        _windowsEscapeHandlerAttached = false;
    }

    private static bool TryGetWindowsContent(out UIElement content)
    {
        content = null!;
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
            return false;

        if (native.Content is not UIElement element)
            return false;

        content = element;
        return true;
    }

    private void OnWindowsPreviewKeyDown(object sender, KeyRoutedEventArgs e) =>
        DispatchWindowsOverlayKey(e, isKeyUp: false);

    private void OnWindowsPreviewKeyUp(object sender, KeyRoutedEventArgs e) =>
        DispatchWindowsOverlayKey(e, isKeyUp: true);

    private void DispatchWindowsOverlayKey(KeyRoutedEventArgs e, bool isKeyUp)
    {
        if (!_playerService.IsVisible)
            return;

        var key = MapWindowsVirtualKey(e.Key);
        if (key is null)
            return;

        var isRepeat = !isKeyUp && e.KeyStatus.WasKeyDown;
        if (isRepeat && key is "arrowleft" or "arrowright")
        {
            e.Handled = true;
            return;
        }

        if (TryHandleNativeVideoKey(key, isKeyUp))
        {
            e.Handled = true;
            return;
        }

        if (key == "escape" && !isKeyUp)
        {
            e.Handled = true;
            if (_playerService is Services.PlayerService playerService)
                playerService.OnBackPressed();
            else
                DispatchBackAsEscape();
        }
    }

    private static string? MapWindowsVirtualKey(VirtualKey key) =>
        key switch
        {
            VirtualKey.Escape => "escape",
            VirtualKey.Space => "space",
            VirtualKey.Enter => "enter",
            VirtualKey.Left => "arrowleft",
            VirtualKey.Right => "arrowright",
            VirtualKey.Up => "arrowup",
            VirtualKey.Down => "arrowdown",
            VirtualKey.F => "f",
            VirtualKey.M => "m",
            VirtualKey.F11 => "f",
            VirtualKey.GoBack => "goback",
            (VirtualKey)0xB3 => "mediaplaypause",
            (VirtualKey)0xB2 => "mediastop",
            _ => null
        };

    private Task OnWindowsEnterFullScreen()
    {
        WindowGeometryPersistence.SetFullscreen(true);
        _playerService.IsFullScreen = true;
        return Task.CompletedTask;
    }

    private Task OnWindowsExitFullScreen()
    {
        WindowGeometryPersistence.SetFullscreen(false);
        _playerService.IsFullScreen = false;
        return Task.CompletedTask;
    }

    internal bool TryEvaluateWebViewJs(string script)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 webView2)
                return false;

            if (webView2.CoreWebView2 is null)
                return false;

            _ = webView2.CoreWebView2.ExecuteScriptAsync(script);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif
