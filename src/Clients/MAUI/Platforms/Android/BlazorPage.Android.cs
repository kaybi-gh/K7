using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.MAUI.Platforms.Android;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private Android.Views.ViewTreeObserver.IOnGlobalFocusChangeListener? _videoFocusBounceListener;
    private bool _videoFocusBounceAttached;
    private string? _directTrackOverrideUrl;
    private int _androidHttpTimeoutRetryCount;
    private int _androidDirectPlayRuntimeRetryCount;
    private DefaultHttpDataSource.Factory? _exoHttpDataSourceFactory;
    private Dictionary<string, string>? _exoHttpRequestHeaders;
    private ExoPlaybackBridge? _exoPlaybackBridge;

    partial void InitializePlayerPlatform()
    {
        _playerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
        _playerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
    }

    /// <summary>
    /// While video is visible with native chrome, always consume DPAD (never leak to WebView).
    /// </summary>
    internal bool TryForwardTvVideoDpad(Android.Views.KeyEvent e)
    {
        if (!_playerService.IsVisible)
            return false;

        if (MauiNativeVideoChrome.IsEnabled)
        {
            var key = MapAndroidDpadKey(e);
            if (key is null)
            {
                // Unknown directional-ish codes: still swallow so Android View focus cannot
                // jump onto transport Buttons underneath the next-episode offer.
                NativeVideoDebug.Log("Dpad unmapped keyCode=" + (int)e.KeyCode + " consumed");
                return true;
            }

            var isKeyUp = e.Action == Android.Views.KeyEventActions.Up;
            if (e.Action == Android.Views.KeyEventActions.Down && e.RepeatCount > 0)
                return true;

            // Always consume while native chrome owns video - even if a specific key is a no-op.
            NativeVideoDebug.Log(
                "Dpad key=" + key + " up=" + isKeyUp + " repeat=" + e.RepeatCount
                + " focusWeb=" + HasWebViewWindowFocus()
                + " nep=" + (_nativeOverlay?.IsNextEpisodeOfferVisible == true)
                + " modal=" + (_nativeOverlay?.IsInputModalActive == true));
            _ = TryHandleNativeVideoKey(key, isKeyUp);
            return true;
        }

        NotifyTvRemoteDpad(e);
        return true;
    }

    private static string? MapAndroidDpadKey(Android.Views.KeyEvent e) =>
        e.KeyCode switch
        {
            Android.Views.Keycode.DpadLeft or Android.Views.Keycode.SystemNavigationLeft => "dpad_left",
            Android.Views.Keycode.DpadRight or Android.Views.Keycode.SystemNavigationRight => "dpad_right",
            Android.Views.Keycode.DpadUp or Android.Views.Keycode.SystemNavigationUp => "dpad_up",
            Android.Views.Keycode.DpadDown or Android.Views.Keycode.SystemNavigationDown => "dpad_down",
            _ => null
        };

    internal bool HasWebViewWindowFocus()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
                return false;
            return webView.IsFocused || webView.HasFocus;
        }
        catch
        {
            return false;
        }
    }


    internal void EnsureVideoSurfaceNotFocusable()
    {
        SuppressNativeVideoFocus();
        try
        {
            // MediaElement remaps PlayerView on layout / first frame and can restore
            // Focusable=true after the one-shot call at open.
            if (NativePlayer.Handler?.PlatformView is Android.Views.View view)
                view.Post(SuppressNativeVideoFocus);
        }
        catch
        {
        }
    }

    private void OnNativeVideoFirstFrame()
    {
        EnsureVideoSurfaceNotFocusable();

        var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
        var playerView = platformView is null ? null : FindPlayerView(platformView);
        AndroidExoHlsTuning.ReapplyHardwareOverlayFlatten(playerView);
        LogVideoSurfaceSnapshot("first-frame");

        _nativeOverlay?.NotifyFirstFrameReady();
    }

    internal void LogVideoSurfaceSnapshot(string reason, VisualElement? extra = null)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            var current = activity?.CurrentFocus;
            var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
            var playerView = platformView is null ? null : FindPlayerView(platformView);
            var surface = playerView?.VideoSurfaceView;
            var subtitle = playerView?.SubtitleView;
            var extraView = extra?.Handler?.PlatformView as Android.Views.View;
            var overlayView = _nativeOverlay?.Handler?.PlatformView as Android.Views.View;
            var webView = blazorWebView.Handler?.PlatformView as global::Android.Webkit.WebView;

            NativeVideoDebug.Warn(
                "Snap " + reason
                + " " + AndroidExoPlaybackStats.FormatCountersLine()
                + " chrome=" + (_nativeOverlay?.IsChromeVisible == true)
                + " currentFocus=" + DescribeAndroidView(current)
                + " player=" + DescribeAndroidView(playerView)
                + " surface=" + DescribeAndroidView(surface)
                + " subtitle=" + DescribeAndroidView(subtitle)
                + " extra=" + DescribeAndroidView(extraView)
                + " overlay=" + DescribeAndroidView(overlayView)
                + " webView=" + DescribeAndroidView(webView)
                + " webMauiVis=" + blazorWebView.IsVisible);

            if (reason.Contains("settings", StringComparison.Ordinal)
                || reason == "chrome-hide"
                || reason == "first-frame")
            {
                LogFocusableChildren("player", playerView);
                LogFocusableChildren("extra", extraView);
            }
        }
        catch (Exception ex)
        {
            NativeVideoDebug.Warn("Snap " + reason + " fail " + ex.GetType().Name);
        }
    }

    private static string DescribeAndroidView(Android.Views.View? view)
    {
        if (view is null)
            return "null";

        var name = view.Class?.SimpleName ?? view.GetType().Name;
        return name
            + " " + view.Width + "x" + view.Height
            + " vis=" + view.Visibility
            + " foc=" + view.Focusable
            + " has=" + view.HasFocus
            + " win=" + view.HasWindowFocus
            + " shown=" + view.IsShown;
    }

    private static void LogFocusableChildren(string label, Android.Views.View? root)
    {
        if (root is null)
            return;

        var n = 0;
        void Walk(Android.Views.View view)
        {
            if (n >= 24)
                return;

            if (view.Focusable || view.HasFocus)
            {
                n++;
                NativeVideoDebug.Warn("  focusable[" + label + "] " + DescribeAndroidView(view));
            }

            if (view is not Android.Views.ViewGroup group)
                return;

            for (var i = 0; i < group.ChildCount && n < 24; i++)
            {
                var child = group.GetChildAt(i);
                if (child is not null)
                    Walk(child);
            }
        }

        Walk(root);
    }

    internal bool TryEvaluateWebViewJs(string script)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
                return false;

            webView.EvaluateJavascript(script, null);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void NotifyTvRemoteDpad(Android.Views.KeyEvent e)
    {
        var arrow = e.KeyCode switch
        {
            Android.Views.Keycode.DpadLeft => "ArrowLeft",
            Android.Views.Keycode.DpadRight => "ArrowRight",
            Android.Views.Keycode.DpadUp => "ArrowUp",
            Android.Views.Keycode.DpadDown => "ArrowDown",
            _ => null
        };
        if (arrow is null)
            return;

        // Ignore native key-repeat: one down starts a JS hold interval, up stops it.
        // Flooding EvaluateJavascript with repeats makes scrub continue long after release.
        if (e.Action == Android.Views.KeyEventActions.Down && e.RepeatCount > 0)
            return;

        var keyCode = (int)e.KeyCode;
        var arrowJson = System.Text.Json.JsonSerializer.Serialize(arrow);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            string script;
            if (e.Action == Android.Views.KeyEventActions.Up)
            {
                script = "try{if(window.K7&&K7.tvDpadHoldStop)K7.tvDpadHoldStop(true);}catch(e){}";
            }
            else
            {
                script =
                    "try{if(window.K7&&K7.tvDpadHoldStart)K7.tvDpadHoldStart("
                    + arrowJson
                    + ","
                    + keyCode
                    + ");else if(window.K7&&K7.dispatchTvArrowKey)K7.dispatchTvArrowKey("
                    + arrowJson
                    + ",'keydown',"
                    + keyCode
                    + ",false);}catch(e){}";
            }

            if (!TryEvaluateWebViewJs(script))
                return;
        });
    }


    partial void ConfigureNativeVideoPlayerAfterOpen()
    {
        _directTrackOverrideUrl = null;
        var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
        var playerView = platformView is null ? null : FindPlayerView(platformView);
        AndroidExoHlsTuning.TryInstallTunedPlayer(NativePlayer, playerView);

        var player = GetPlayer(NativePlayer);
        ApplyAndroidHlsAvSyncSettings(player);
        AttachExoPlaybackBridge(player);
        if (player is IExoPlayer exo)
        {
            AndroidExoHlsTuning.ApplyPlaybackSurfaceTuning(exo, playerView);
            TryPublishExoTimelineFromPlayer(exo);
            ApplyPendingAndroidSubtitleStyle();
        }
        SetVideoFocusOwnership(active: true);
    }

    internal void ApplyPendingAndroidSubtitleStyle()
    {
        var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
        var playerView = platformView is null ? null : FindPlayerView(platformView);
        AndroidExoSubtitleStyle.Apply(playerView, AndroidSubtitleStyle.GetSettings());
    }

    partial void DetachPlayerPlatform()
    {
        _directTrackOverrideUrl = null;
        if (_exoPlaybackBridge is not null)
        {
            _exoPlaybackBridge.FirstFrameRendered = null;
            _exoPlaybackBridge.TracksChanged = null;
            _exoPlaybackBridge.PositionHeard = null;
            _exoPlaybackBridge.DurationHeard = null;
            _exoPlaybackBridge.PlaybackStateHeard = null;
            _exoPlaybackBridge.PlaybackErrorHeard = null;
            _exoPlaybackBridge.BufferedHeard = null;
        }
        _exoPlaybackBridge?.Detach();
        AndroidExoPlaybackStats.Detach();
        AndroidDisplayAfr.Restore();
    }

    // LibVLC Android removed; ExoPlayer owns Direct/HLS/local.
    internal void ReleaseSidecarTextSubtitles() { }
    internal void NotifySidecarTextSubtitles(bool ready) { }

    private void BindAndroidExoPlayerWithLongHttpTimeouts(string url)
    {
        if (LocalPlaybackUrl.IsLocalFile(url))
            return;

        const int connectTimeoutMs = 60_000;
        const int readTimeoutMs = 120_000;

        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return;

            var httpFactory = GetOrCreateExoHttpDataSourceFactory(connectTimeoutMs, readTimeoutMs);
            ApplyExoPlayerHttpAuthHeaders();

            var mediaItem = MediaItem.FromUri(url)!;
            var mediaSource = AndroidExoHlsTuning.CreateStreamingMediaSource(httpFactory, mediaItem, url);
            if (mediaSource is null)
                return;

            exo.PlayWhenReady = true;
            exo.SetMediaSource(mediaSource);
            exo.Prepare();
            ApplyAndroidHlsAvSyncSettings(exo);
            AttachExoPlaybackBridge(exo);
            TryPublishExoTimelineFromPlayer(exo);
            ApplyPendingAndroidSubtitleStyle();
            _androidHttpTimeoutRetryCount = 0;

            // Do not PostDelayed SetMediaSource again: a second Prepare resets HLS to
            // startSeconds/segment boundaries (~1015s jumps), fights PendingSeek, and blinks.
            // Long timeouts are already on this bind; MediaFailed auth path rebinds explicitly.
        }
        catch
        {
        }
    }

    /// <summary>
    /// Drive HTTP Direct Play / HLS from the tuned Exo instance. Skip MediaElement.Source so
    /// CommunityToolkit does not open a second pipeline and tick IPlayerListener on the UI thread.
    /// Local files still use MediaElement.FromUri(file://).
    /// </summary>
    private void OpenAndroidExoSource(string url)
    {
        NativePlayer.ShouldAutoPlay = true;
        var needToolkitSource = LocalPlaybackUrl.IsLocalFile(url)
            || NativePlayer.Handler?.PlatformView is null;

        NativePlayer.Stop();
        if (needToolkitSource)
            NativePlayer.Source = CreateMediaSourceWithAuth(url);

        NativeVideoDebug.Log(
            "OpenNativePlayerSource local=" + LocalPlaybackUrl.IsLocalFile(url)
            + " host=exo toolkitSource=" + needToolkitSource
            + " url=" + (LocalPlaybackUrl.IsLocalFile(url) ? "file" : "http"));
        ConfigureNativeVideoPlayerAfterOpen();
        BindAndroidExoPlayerWithLongHttpTimeouts(url);
        if (!TrySetAndroidVideoPlayWhenReady(true))
            NativePlayer.Play();
    }

    internal bool TrySetAndroidVideoPlayWhenReady(bool play)
    {
        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return false;

            exo.PlayWhenReady = play;
            if (play)
                exo.Play();
            else
                exo.Pause();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool TryStopAndroidVideo()
    {
        try
        {
            SuppressAndroidPlayerViewPlaceholder();
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return false;

            exo.PlayWhenReady = false;
            exo.Stop();
            exo.ClearMediaItems();
            SuppressAndroidPlayerViewPlaceholder();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal void SuppressAndroidPlayerViewPlaceholder()
    {
        try
        {
            var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
            var playerView = platformView is null ? null : FindPlayerView(platformView);
            AndroidExoHlsTuning.SuppressPlayerViewPlaceholder(playerView);
        }
        catch
        {
        }
    }

    internal bool TrySetAndroidVideoSpeed(double rate)
    {
        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return false;

            var speed = (float)Math.Clamp(rate, 0.25, 4);
            // Compressed audio offload cannot be time-stretched, so on Direct Play (offloaded
            // original track) a non-1x rate is silently ignored. Drop offload while speeding
            // so the decoded PCM + Sonic path applies the rate; restore it at 1x.
            AndroidExoHlsTuning.SetAudioOffloadForSpeed(exo, speed);
            var pitch = exo.PlaybackParameters?.Pitch ?? 1f;
            exo.PlaybackParameters = new PlaybackParameters(speed, pitch);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool IsAndroidExoHostActive() => UnwrapPlayer(GetPlayer(NativePlayer)) is IExoPlayer;

    internal bool AndroidExoPlayerHasError()
    {
        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            return player?.PlayerError is not null;
        }
        catch
        {
            return false;
        }
    }

    private DefaultHttpDataSource.Factory GetOrCreateExoHttpDataSourceFactory(int connectTimeoutMs, int readTimeoutMs)
    {
        if (_exoHttpDataSourceFactory is not null)
            return _exoHttpDataSourceFactory;

        _exoHttpRequestHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        _exoHttpDataSourceFactory = new DefaultHttpDataSource.Factory()!
            .SetConnectTimeoutMs(connectTimeoutMs)!
            .SetReadTimeoutMs(readTimeoutMs)!
            .SetAllowCrossProtocolRedirects(true)!;
        return _exoHttpDataSourceFactory;
    }

    /// <summary>
    /// Push the current Bearer into the shared Exo HTTP factory. Segment requests created after
    /// this call pick up the new token without SetMediaSource (avoids periodic buffer stalls).
    /// </summary>
    private void ApplyExoPlayerHttpAuthHeaders()
    {
        _exoHttpRequestHeaders ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var authValue = ResolveNativePlayerAuthorizationHeader();
        if (!string.IsNullOrEmpty(authValue))
            _exoHttpRequestHeaders["Authorization"] = authValue;
        else
            _exoHttpRequestHeaders.Remove("Authorization");

        _exoHttpDataSourceFactory?.SetDefaultRequestProperties(_exoHttpRequestHeaders);
    }

    /// <summary>
    /// Rebind ExoPlayer with the current access token and seek back so a token rotation
    /// (or 401 MediaFailed) does not restart the movie at 0:00.
    /// </summary>
    private void RebindAndroidNativeVideoPreservingPosition(string url, double resumeAtSeconds)
    {
        if (resumeAtSeconds > 1 && _playerService.Source is { } source)
            source.PendingSeekTime = resumeAtSeconds;

        BindAndroidExoPlayerWithLongHttpTimeouts(url);
        if (!TrySetAndroidVideoPlayWhenReady(true))
            NativePlayer.Play();

        if (resumeAtSeconds > 1)
            SeekNativeVideoAsync(resumeAtSeconds).FireAndForget();
    }

    private async Task RetryAndroidDirectPlayAfterRuntimeCheckAsync(string url, double resumeAt)
    {
        try
        {
            await Task.Delay(400);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_playerService.IsVisible)
                    return;
                if (!string.Equals(_playerService.Source?.Url, url, StringComparison.Ordinal))
                    return;

                NativeVideoDebug.Log("RetryDirectPlay after 1004 resumeAt=" + resumeAt.ToString("F1"));
                RebindAndroidNativeVideoPreservingPosition(url, resumeAt);
                ReportNativePlaybackIssue(
                    "NativePlayer.DirectPlayRuntimeRetry",
                    "resumeAt=" + resumeAt.ToString("F1") + "s url=" + RedactUrl(url));
            });
        }
        catch
        {
        }
    }

    private void BindAndroidExoPlayerWithLongHttpTimeoutsCore(string url, int connectTimeoutMs, int readTimeoutMs)
    {
        if (LocalPlaybackUrl.IsLocalFile(url))
            return;

        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return;

            // Fresh factory for retry path (previous bind may have failed mid-setup).
            _exoHttpDataSourceFactory = null;
            var httpFactory = GetOrCreateExoHttpDataSourceFactory(connectTimeoutMs, readTimeoutMs);
            ApplyExoPlayerHttpAuthHeaders();

            var mediaItem = MediaItem.FromUri(url)!;
            var mediaSource = AndroidExoHlsTuning.CreateStreamingMediaSource(httpFactory, mediaItem, url);
            if (mediaSource is null)
                return;

            exo.PlayWhenReady = true;
            exo.SetMediaSource(mediaSource);
            exo.Prepare();
            ApplyAndroidHlsAvSyncSettings(exo);
            AttachExoPlaybackBridge(exo);
        }
        catch (Exception)
        {
            // Best-effort reassert - leave the previous bind in place.
        }
    }


    private double GetExoPlaybackPositionSeconds()
    {
        try
        {
            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is not IExoPlayer exo)
                return 0;

            var posMs = exo.CurrentPosition;
            return posMs > 0 ? posMs / 1000.0 : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Seek via ExoPlayer with PREVIOUS_SYNC + segment-aligned target. MediaElement.SeekTo uses
    /// exact mid-GOP seeks; on HLS that leaves a frozen TextureView frame while audio plays
    /// until the next independent segment.
    /// </summary>
    private Task SeekAndroidVideoAsync(double positionSeconds) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var resumePlayback = _playerService.PlaybackState
                is Server.Domain.Enums.PlaybackState.Playing
                or Server.Domain.Enums.PlaybackState.Buffering;

            var targetSeconds = Math.Max(0, positionSeconds);
            var duration = _playerService.Duration > 0
                ? _playerService.Duration
                : NativePlayer.Duration.TotalSeconds;
            if (duration > 0)
                targetSeconds = Math.Min(targetSeconds, duration);

            // No-op seeks (auth rebind, soft-subtitle re-apply) still pause ExoPlayer.
            var currentPos = GetExoPlaybackPositionSeconds();
            if (currentPos <= 0)
                currentPos = NativePlayer.Position.TotalSeconds;
            if (currentPos > 0 && Math.Abs(currentPos - targetSeconds) < 0.75)
            {
                if (_playerService.Source is { } nearSource)
                    nearSource.PendingSeekTime = null;
                NativeVideoDebug.Log(
                    "SeekAndroid skip near-current target=" + targetSeconds.ToString("F1")
                    + "s pos=" + currentPos.ToString("F1") + "s");
                return;
            }

            // Do not floor to a fake 6s grid: video playlists use keyframe-aligned EXTINF.
            // PREVIOUS_SYNC + INDEPENDENT-SEGMENTS snaps to the real segment start.
            RememberSeekTarget(targetSeconds);
            NativeVideoDebug.Log(
                "SeekAndroid target=" + targetSeconds.ToString("F1")
                + "s resumePlay=" + resumePlayback
                + " state=" + NativePlayer.CurrentState
                + " pos=" + NativePlayer.Position.TotalSeconds.ToString("F1") + "s");


            var player = UnwrapPlayer(GetPlayer(NativePlayer));
            if (player is null)
            {
                _ = SeekMediaElementAsync(
                    NativePlayer,
                    TimeSpan.FromSeconds(targetSeconds),
                    () => _playerService.PlaybackState,
                    t => _playerService.CurrentTime = t,
                    () => OnAfterNativeVideoSeek());
                return;
            }

            EnsureVideoSurfaceNotFocusable();
            ApplyAndroidHlsAvSyncSettings(player);

            // Seek the PlayerView Exo instance. MediaElement.SeekTo can target a stale toolkit
            // reference when PlayerView.Player was replaced, and IExoPlayerInvoker exact-seeks.
            try
            {
                player.SeekTo((long)(targetSeconds * 1000.0));
            }
            catch (Exception)
            {
                try
                {
                    NativePlayer.SeekTo(TimeSpan.FromSeconds(targetSeconds));
                }
                catch (Exception)
                {
                    _ = SeekMediaElementAsync(
                        NativePlayer,
                        TimeSpan.FromSeconds(targetSeconds),
                        () => _playerService.PlaybackState,
                        t => _playerService.CurrentTime = t,
                        () => OnAfterNativeVideoSeek());
                    return;
                }
            }

            // Toolkit SeekTo can reset SeekParameters on some versions - re-apply before Play.
            ApplyAndroidHlsAvSyncSettings(player);

            _playerService.CurrentTime = targetSeconds;

            // Always resume when we interrupted play for scrub/resume. Checking CurrentState
            // alone can skip Play() while ExoPlayer is paused mid-seek (frozen last frame).
            if (resumePlayback && !TrySetAndroidVideoPlayWhenReady(true))
                NativePlayer.Play();

            // Soft invalidate only - never null PlayerView.Player (mutes / freezes TextureView).
            TryInvalidateVideoSurface();
            OnAfterNativeVideoSeek();
        });

    private void TryInvalidateVideoSurface()
    {
        try
        {
            var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
            if (platformView is null)
                return;

            var playerView = FindPlayerView(platformView);
            playerView?.Invalidate();
            platformView.Invalidate();
        }
        catch
        {
        }
    }

    private static IPlayer? UnwrapPlayer(IPlayer? player)
    {
        if (player is null)
            return null;

        try
        {
            if (player is not Java.Lang.Object javaObj)
                return player;

            // Always walk wrappers. IExoPlayerInvoker implements IExoPlayer but SeekParameters
            // set on the invoker may not reach ExoPlayerImpl (frozen frame after seek).
            for (var depth = 0; depth < 8; depth++)
            {
                var advanced = false;
                var beforeType = player.GetType().Name;

                foreach (var methodName in new[] { "getWrappedPlayer", "getPlayer", "getInternalPlayer" })
                {
                    Java.Lang.Reflect.Method? method = null;
                    try
                    {
                        method = javaObj.Class.GetMethod(methodName);
                    }
                    catch (Java.Lang.NoSuchMethodException)
                    {
                    }

                    if (method is null)
                        continue;

                    var wrapped = method.Invoke(javaObj);
                    if (wrapped is IPlayer next && !ReferenceEquals(next, player))
                    {
                        player = next;
                        if (next is Java.Lang.Object nextObj)
                            javaObj = nextObj;
                        advanced = true;
                        break;
                    }
                }

                if (!advanced)
                {
                    // Scan declared fields for nested IPlayer (Toolkit wrappers vary by version).
                    for (var cls = javaObj.Class; cls is not null && !advanced; cls = cls.Superclass)
                    {
                        Java.Lang.Reflect.Field[]? fields;
                        try
                        {
                            fields = cls.GetDeclaredFields();
                        }
                        catch
                        {
                            break;
                        }

                        if (fields is null)
                            break;

                        foreach (var field in fields)
                        {
                            try
                            {
                                field.Accessible = true;
                                if (field.Get(javaObj) is IPlayer inner && !ReferenceEquals(inner, player))
                                {
                                    player = inner;
                                    if (inner is Java.Lang.Object innerObj)
                                        javaObj = innerObj;
                                    advanced = true;
                                    break;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                if (!advanced)
                    break;
            }
        }
        catch
        {
        }

        return player;
    }
partial void OnAfterNativeVideoSeek()
    {
        EnsureVideoSurfaceNotFocusable();
        if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible)
            return;

        if (!HasWebViewWindowFocus())
            BounceWindowFocusToWebView();
    }


    private void SetVideoFocusOwnership(bool active)
    {
        EnsureVideoSurfaceNotFocusable();

        if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible)
        {
            // Native XAML chrome owns input - do not bounce window focus into the (hidden)
            // WebView. Keep the global-focus listener so PlayerView/SurfaceView cannot hold
            // window focus (Amlogic drops HEVC frames when the video surface is focused).
            AttachVideoFocusBounceListener();
            SuppressWebViewFocusForNativeChrome();
            return;
        }

        if (active)
        {
            AttachVideoFocusBounceListener();
            BounceWindowFocusToWebView();
        }
        else
        {
            DetachVideoFocusBounceListener();
            BounceWindowFocusToWebView();
        }
    }

    private void SuppressWebViewFocusForNativeChrome()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
                return;

            webView.Focusable = false;
            webView.FocusableInTouchMode = false;
            if (webView.IsFocused)
                webView.ClearFocus();
        }
        catch
        {
        }
    }

    private void AttachVideoFocusBounceListener()
    {
        if (_videoFocusBounceAttached)
            return;

        try
        {
            var activity = Platform.CurrentActivity;
            var decor = activity?.Window?.DecorView;
            var observer = decor?.ViewTreeObserver;
            if (observer is null || !observer.IsAlive)
                return;

            _videoFocusBounceListener ??= new VideoFocusBounceListener(this);
            observer.AddOnGlobalFocusChangeListener(_videoFocusBounceListener);
            _videoFocusBounceAttached = true;
        }
        catch (Exception)
        {
        }
    }

    private void DetachVideoFocusBounceListener()
    {
        if (!_videoFocusBounceAttached)
            return;

        try
        {
            var activity = Platform.CurrentActivity;
            var decor = activity?.Window?.DecorView;
            var observer = decor?.ViewTreeObserver;
            if (observer is not null && observer.IsAlive && _videoFocusBounceListener is not null)
                observer.RemoveOnGlobalFocusChangeListener(_videoFocusBounceListener);
        }
        catch
        {
        }

        _videoFocusBounceAttached = false;
    }

    private void OnVideoGlobalFocusChanged(Android.Views.View? oldFocus, Android.Views.View? newFocus)
    {
        if (!_playerService.IsVisible)
            return;

        NativeVideoDebug.Warn(
            "Focus " + DescribeAndroidView(oldFocus)
            + " -> " + DescribeAndroidView(newFocus)
            + " " + AndroidExoPlaybackStats.FormatCountersLine());

        if (MauiNativeVideoChrome.IsEnabled)
        {
            // Software DPAD rings on the overlay. If the video surface grabbed window focus
            // (chrome hidden + overlay GONE, or MediaElement remapped Focusable), strip it.
            // Do not RequestFocus the hidden WebView: that steals TV keys from native chrome.
            if (newFocus is not null && IsNativePlayerDescendant(newFocus))
                EnsureVideoSurfaceNotFocusable();
            return;
        }

        if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
            return;

        // Paint-only video: never allow PlayerView/TextureView (or anything else) to keep
        // window focus while Blazor owns the HUD. Do not touch DOM focus.
        if (newFocus is null || IsDescendantOf(webView, newFocus))
            return;

        EnsureVideoSurfaceNotFocusable();
        BounceWindowFocusToWebView();
    }

    private bool IsNativePlayerDescendant(Android.Views.View node)
    {
        try
        {
            if (NativePlayer.Handler?.PlatformView is not Android.Views.View root)
                return false;
            return IsDescendantOf(root, node);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDescendantOf(Android.Views.View root, Android.Views.View? node)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;
            current = current.Parent as Android.Views.View;
        }

        return false;
    }

    /// <summary>
    /// Restore Android window focus to the WebView without wiping document.activeElement
    /// (never EvaluateJavascript overlay.focus - that breaks OK on close/play).
    /// </summary>
    private void BounceWindowFocusToWebView()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
                return;

            webView.Focusable = true;
            webView.FocusableInTouchMode = true;
            if (!webView.IsFocused)
                webView.RequestFocus();
        }
        catch
        {
        }
    }


    private void SuppressNativeVideoFocus()
    {
        try
        {
            if (NativePlayer.Handler?.PlatformView is not Android.Views.View leftover)
                return;

            leftover.ClearFocus();
            DisableFocusRecursive(leftover);
            leftover.ClearFocus();
        }
        catch
        {
        }
    }

    private static void DisableFocusRecursive(Android.Views.View? view)
    {
        if (view is null)
            return;

        view.Focusable = false;
        view.FocusableInTouchMode = false;
        if (view is Android.Views.ViewGroup group)
        {
            group.DescendantFocusability = Android.Views.DescendantFocusability.BlockDescendants;
            for (var i = 0; i < group.ChildCount; i++)
                DisableFocusRecursive(group.GetChildAt(i));
        }
    }


    private sealed class VideoFocusBounceListener(BlazorPage page)
        : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnGlobalFocusChangeListener
    {
        public void OnGlobalFocusChanged(Android.Views.View? oldFocus, Android.Views.View? newFocus)
            => page.OnVideoGlobalFocusChanged(oldFocus, newFocus);
    }

    private void AttachExoPlaybackBridge(IPlayer? player)
    {
        player = UnwrapPlayer(player);
        if (player is not IExoPlayer exo)
            return;

        var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
        var playerView = platformView is null ? null : FindPlayerView(platformView);

        _exoPlaybackBridge ??= new ExoPlaybackBridge();
        _exoPlaybackBridge.FirstFrameRendered = () =>
        {
            TryApplyHdmiAutoFrameRate();
            MainThread.BeginInvokeOnMainThread(OnNativeVideoFirstFrame);
        };
        _exoPlaybackBridge.TracksChanged = () => MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplySelectedTrackOverrides();
            TryApplyHdmiAutoFrameRate();
        });
        _exoPlaybackBridge.PositionHeard = OnExoPositionHeard;
        _exoPlaybackBridge.DurationHeard = OnExoDurationHeard;
        _exoPlaybackBridge.PlaybackStateHeard = OnExoPlaybackStateHeard;
        _exoPlaybackBridge.PlaybackErrorHeard = OnExoPlaybackErrorHeard;
        _exoPlaybackBridge.BufferedHeard = OnExoBufferedHeard;
        _exoPlaybackBridge.Attach(exo, playerView);
        AndroidExoPlaybackStats.Attach(exo);
        TryPublishExoTimelineFromPlayer(exo);
        ApplySelectedTrackOverrides();
        TryApplyHdmiAutoFrameRate();
    }

    private void TryApplyHdmiAutoFrameRate()
    {
        if (!_playerService.IsVisible)
            return;

        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        var mode = AndroidDisplayAfr.ResolveMode();
        if (!AndroidExoPlaybackPolicy.ShouldApplyHdmiAutoFrameRate(
                mode,
                AndroidExoHlsTuning.IsAndroidTelevision(),
                manufacturer,
                model))
        {
            AndroidDisplayAfr.Restore();
            return;
        }

        var fps = _playerService.Source?.SourceFrameRate ?? 0f;
        var player = UnwrapPlayer(GetPlayer(NativePlayer));
        var exo = player as IExoPlayer;
        if (fps <= 1f && exo is not null)
            fps = AndroidExoPlaybackStats.TryReadFrameRate(exo);
        if (fps <= 1f)
            return;

        NativeVideoDebug.Log(
            "HdmiAfr mode=" + HdmiAutoFrameRatePolicy.Persist(mode)
            + " fps=" + fps.ToString("F3"));
        AndroidDisplayAfr.Apply(
            fps,
            _playerService.Source?.SourceVideoWidth ?? 0,
            _playerService.Source?.SourceVideoHeight ?? 0,
            AndroidExoPlaybackPolicy.ShouldPreferContentHdmiResolution(mode));
    }

    private void OnExoDurationHeard(double seconds)
    {
        if (!_playerService.IsVisible || seconds <= 0)
            return;

        if (Math.Abs(seconds - _playerService.Duration) > 0.5)
            _playerService.Duration = seconds;

        _androidPendingSeekNudge?.Invoke();
    }

    private void TryPublishExoTimelineFromPlayer(IExoPlayer exo)
    {
        try
        {
            var durMs = exo.Duration;
            if (durMs > 0 && durMs < 864_000_000_000L)
                OnExoDurationHeard(durMs / 1000.0);

            var posMs = exo.CurrentPosition;
            if (posMs > 0)
                OnExoPositionHeard(posMs / 1000.0);

            var bufferedMs = exo.BufferedPosition;
            if (bufferedMs > 0)
                OnExoBufferedHeard(bufferedMs / 1000.0);
        }
        catch
        {
        }
    }

    private void OnExoBufferedHeard(double seconds)
    {
        if (!_playerService.IsVisible || seconds < 0)
            return;

        _playerService.BufferedTime = seconds;
    }

    private void OnExoPositionHeard(double seconds)
    {
        if (!_playerService.IsVisible || seconds <= 0)
            return;

        if (_chainedSeekTargetSeconds is double chained
            && (DateTime.UtcNow - _chainedSeekUtc).TotalMilliseconds < 900
            && Math.Abs(seconds - chained) > 2)
        {
            return;
        }

        _playerService.CurrentTime = seconds;
    }

    private void OnExoPlaybackStateHeard(int exoState, bool playWhenReady, bool isPlaying)
    {
        if (!_playerService.IsVisible)
            return;

        var mapped = ExoPlaybackStateMapping.Map(exoState, playWhenReady, isPlaying);
        _playerService.PlaybackState = mapped;
        NativeVideoDebug.Log(
            "ExoState mapped=" + mapped
            + " exo=" + exoState
            + " playWhenReady=" + playWhenReady
            + " isPlaying=" + isPlaying
            + " pos=" + GetExoPlaybackPositionSeconds().ToString("F2")
            + "s visible=" + _playerService.IsVisible
            + " pending=" + (_playerService.Source?.PendingSeekTime?.ToString("F1") ?? "null"));

        if (MauiNativeVideoChrome.IsEnabled)
        {
            if (mapped == Server.Domain.Enums.PlaybackState.Playing)
            {
                var pending = _playerService.Source?.PendingSeekTime;
                var pos = GetExoPlaybackPositionSeconds();
                if (pending is double resumeAt && resumeAt > 1 && pos < resumeAt - 2)
                    _nativeOverlay?.SetLoadingVeil(true);
            }
            else if (mapped is Server.Domain.Enums.PlaybackState.Buffering
                or Server.Domain.Enums.PlaybackState.Idle)
            {
                var pos = GetExoPlaybackPositionSeconds();
                if (pos <= 1)
                    _nativeOverlay?.SetLoadingVeil(true);
            }
        }

        _androidPendingSeekNudge?.Invoke();
    }

    private void OnExoPlaybackErrorHeard(string detail)
    {
        HandleNativeVideoMediaFailed(detail);
    }

    private static void ApplyAndroidHlsAvSyncSettings(IPlayer? player)
    {
        TryApplyPreviousSyncSeekParameters(player);
        TryDisableSkipSilence(player);
    }

    private static void TryDisableSkipSilence(IPlayer? player)
    {
        try
        {
            player = UnwrapPlayer(player);
            if (player is IExoPlayer exo)
                exo.SkipSilenceEnabled = false;
        }
        catch
        {
        }
    }

    private static bool TryApplyPreviousSyncSeekParameters(IPlayer? player)
    {
        try
        {
            player = UnwrapPlayer(player);
            if (player is null)
                return false;

            // Prefer JNI setSeekParameters on the concrete Java type. Assigning
            // IExoPlayer.SeekParameters on IExoPlayerInvoker can report success without
            // updating ExoPlayerImpl (exact mid-GOP seek -> frozen TextureView + live audio).
            if (player is Java.Lang.Object javaObj)
            {
                var seekParamsClass = Java.Lang.Class.ForName("androidx.media3.exoplayer.SeekParameters");
                if (seekParamsClass is not null)
                {
                    var previous = seekParamsClass.GetField("PREVIOUS_SYNC")?.Get(null)
                        ?? seekParamsClass.GetDeclaredField("PREVIOUS_SYNC")?.Get(null);
                    if (previous is not null)
                    {
                        for (var cls = javaObj.Class; cls is not null; cls = cls.Superclass)
                        {
                            Java.Lang.Reflect.Method? method = null;
                            try
                            {
                                method = cls.GetMethod("setSeekParameters", seekParamsClass);
                            }
                            catch (Java.Lang.NoSuchMethodException)
                            {
                            }

                            if (method is null)
                            {
                                try
                                {
                                    method = cls.GetDeclaredMethod("setSeekParameters", seekParamsClass);
                                }
                                catch (Java.Lang.NoSuchMethodException)
                                {
                                }
                            }

                            if (method is null)
                                continue;

                            method.Accessible = true;
                            method.Invoke(javaObj, previous);
                            return true;
                        }
                    }
                }
            }

            if (player is IExoPlayer exo)
            {
                exo.SeekParameters = SeekParameters.PreviousSync;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>
    /// Enriches MediaFailed with ExoPlayer error code and HTTP response details when available.
    /// ERROR_CODE_IO_UNSPECIFIED (2000) alone is not enough - ResponseCode distinguishes 401 vs 404.
    /// DataSpecUri identifies which playlist/segment URL failed when the exception carries it.
    /// </summary>
    private string FormatAndroidPlayerErrorDetail()
    {
        try
        {
            var player = GetPlayer(NativePlayer);
            if (player?.PlayerError is not { } error)
                return string.Empty;

            var codeName = PlaybackException.GetErrorCodeName(error.ErrorCode) ?? "(unknown)";
            var detail =
                " PlayerErrorCode="
                + error.ErrorCode
                + " PlayerErrorCodeName="
                + codeName
                + " PlayerErrorMessage="
                + (error.Message ?? "(null)");

            // Parser failures (e.g. Top bit not zero) wrap IllegalStateException without DataSpec.
            // MediaItem URI is the playlist; still useful when segment DataSpec is absent.
            if (TryGetMediaItemUri(player.CurrentMediaItem, out var mediaItemUri)
                && !string.IsNullOrEmpty(mediaItemUri))
            {
                detail += " MediaItemUri=" + RedactUrl(mediaItemUri);
            }

            Java.Lang.Throwable? cause = error.Cause;
            var depth = 0;
            var sawDataSpecUri = false;
            while (cause is not null && depth < 8)
            {
                detail +=
                    " Cause["
                    + depth
                    + "]="
                    + cause.GetType().Name
                    + ": "
                    + (cause.Message ?? "(null)");

                // Xamarin bindings flatten Java nested types (HttpDataSource$InvalidResponseCodeException).
                if (cause is HttpDataSourceInvalidResponseCodeException invalidResponse)
                {
                    detail += " ResponseCode=" + invalidResponse.ResponseCode;
                    var dataSpecUri = invalidResponse.DataSpec?.Uri?.ToString();
                    if (!string.IsNullOrEmpty(dataSpecUri))
                    {
                        detail += " DataSpecUri=" + RedactUrl(dataSpecUri);
                        sawDataSpecUri = true;
                    }
                }
                else if (cause is HttpDataSourceHttpDataSourceException httpEx)
                {
                    var dataSpecUri = httpEx.DataSpec?.Uri?.ToString();
                    if (!string.IsNullOrEmpty(dataSpecUri))
                    {
                        detail += " DataSpecUri=" + RedactUrl(dataSpecUri);
                        sawDataSpecUri = true;
                    }
                }
                else if (!sawDataSpecUri
                    && TryGetDataSpecUriFromCause(cause, out var reflectedUri)
                    && !string.IsNullOrEmpty(reflectedUri))
                {
                    detail += " DataSpecUri=" + RedactUrl(reflectedUri);
                    sawDataSpecUri = true;
                }

                cause = cause.Cause;
                depth++;
            }

            if (!sawDataSpecUri)
                detail += " DataSpecUri=(none-parser-or-non-http)";

            return detail;
        }
        catch (Exception ex)
        {
            return " PlayerErrorDetailFailed=" + ex.Message;
        }
    }

    /// <summary>
    /// Parser/load failures after HTTP 200 often wrap IllegalStateException without a typed
    /// HttpDataSourceException. Reflect DataSpec when the binding exposes it on any cause.
    /// </summary>
    private static bool TryGetDataSpecUriFromCause(Java.Lang.Throwable cause, out string? uri)
    {
        uri = null;
        try
        {
            var dataSpecProp = cause.GetType().GetProperty("DataSpec");
            if (dataSpecProp?.GetValue(cause) is DataSpec dataSpec)
            {
                uri = dataSpec.Uri?.ToString();
                return !string.IsNullOrEmpty(uri);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }

        return false;
    }

    /// <summary>
    /// Xamarin Media3 bindings expose LocalConfiguration as a nested type name that shadows the
    /// instance property, so read the playlist URI via reflection.
    /// </summary>
    private static bool TryGetMediaItemUri(MediaItem? mediaItem, out string? uri)
    {
        uri = null;
        if (mediaItem is null)
            return false;

        try
        {
            var localConfigProp = typeof(MediaItem).GetProperty("LocalConfiguration");
            var localConfig = localConfigProp?.GetValue(mediaItem);
            if (localConfig is null)
                return false;

            var uriProp = localConfig.GetType().GetProperty("Uri");
            uri = uriProp?.GetValue(localConfig)?.ToString();
            return !string.IsNullOrEmpty(uri);
        }
        catch
        {
            return false;
        }
    }

    private static readonly global::Android.Graphics.Color AndroidBrandShell =
        global::Android.Graphics.Color.Rgb(13, 9, 7);

    /// <summary>
    /// WebView sits above MediaElement. Opaque black covers the TV white clear while HLS loads.
    /// True TRANSPARENT is required for TextureView frames to show through; Color.Argb(1,0,0,0)
    /// blocks video on some TV GPUs (black picture / audio-only).
    /// Important: SetBackgroundResource(0) / Background=null clear any ColorDrawable - only use
    /// those when intentionally see-through, never after painting opaque black.
    /// </summary>
    private void ApplyAndroidWebViewShell(bool seeThroughForVideo)
    {
        if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView platformView)
            return;

        if (seeThroughForVideo)
        {
            ApplyAndroidWebViewBackground(platformView, global::Android.Graphics.Color.Transparent, clearDrawable: true);
            blazorWebView.BackgroundColor = Colors.Transparent;
        }
        else
        {
            // Keep the ColorDrawable - do not null it or TV clears white over MediaElement.
            ApplyAndroidWebViewBackground(platformView, global::Android.Graphics.Color.Black, clearDrawable: false);
            blazorWebView.BackgroundColor = Colors.Black;
        }
    }

    private void ApplyAndroidWebViewShellBrand()
    {
        if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView platformView)
            return;

        ApplyAndroidWebViewBackground(platformView, AndroidBrandShell, clearDrawable: false);
        blazorWebView.BackgroundColor = Color.FromRgb(13, 9, 7);
    }

    private static void ApplyAndroidWebViewBackground(
        global::Android.Webkit.WebView platformView,
        global::Android.Graphics.Color color,
        bool clearDrawable)
    {
        platformView.SetBackgroundColor(color);
        if (clearDrawable)
        {
            platformView.SetBackgroundResource(0);
            platformView.Background = null;
        }

        if (platformView.Parent is Android.Views.View parentView)
        {
            parentView.SetBackgroundColor(color);
            if (clearDrawable)
            {
                parentView.SetBackgroundResource(0);
                parentView.Background = null;
            }
        }
    }

    /// <summary>
    /// Escape/Stop can leave body.native-player-active set while MediaElement is already gone,
    /// which hides all WebView chrome (visibility:hidden) and looks like a dead black screen.
    /// </summary>
    private void ClearNativePlayerActiveShell()
    {
        // Must not depend on the Blazor dispatcher - it can be stalled after scrub/seek, which
        // leaves body.native-player-active set and looks like a dead black screen.
        if (TryEvaluateWebViewJs(
                "try{if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(false,false);}catch(e){}"))
        {
            return;
        }

        _ = blazorWebView.TryDispatchAsync(async sp =>
        {
            try
            {
                var js = sp.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("K7.setNativePlayerActive", false, false);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
            {
            }
        });
    }


    private static IPlayer? GetPlayer(MediaElement mediaElement)
    {
        var platformView = mediaElement.Handler?.PlatformView as Android.Views.View;
        if (platformView is null)
            return null;

        var playerView = FindPlayerView(platformView);
        if (playerView is null)
            return null;

        return playerView.Player;
    }

    private static AndroidX.Media3.UI.PlayerView? FindPlayerView(Android.Views.View view)
    {
        if (view is AndroidX.Media3.UI.PlayerView pv)
            return pv;

        if (view is Android.Views.ViewGroup vg)
        {
            for (var i = 0; i < vg.ChildCount; i++)
            {
                var child = vg.GetChildAt(i);
                if (child is null) continue;
                var result = FindPlayerView(child);
                if (result is not null) return result;
            }
        }

        return null;
    }



    private void OnSwitchAudioTrack(string trackName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var player = GetPlayer(NativePlayer);
            if (player is null)
                return;

            var tracks = player.CurrentTracks;
            if (tracks?.Groups is null || tracks.Groups.Size() == 0)
                return;

            for (var i = 0; i < tracks.Groups.Size(); i++)
            {
                var group = (Tracks.Group)tracks.Groups.Get(i)!;
                if (group.Type != C.TrackTypeAudio)
                    continue;

                for (var j = 0; j < group.Length; j++)
                {
                    var format = group.GetTrackFormat(j);
                    if (string.Equals(format?.Label, trackName, StringComparison.OrdinalIgnoreCase)
                        || format?.Language == trackName)
                    {
                        SelectAudioOverride(player, group, j);
                        return;
                    }
                }
            }

            // Direct / HLS: trackName is often audio-{fileStreamIndex}
            if (trackName.StartsWith("audio-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(trackName.AsSpan(6), out var fileStreamIndex))
            {
                var ordered = _playerService.AudioTracks.OrderBy(t => t.Index).ToList();
                var catalog = ordered.FirstOrDefault(t => t.Index == fileStreamIndex);
                var ordinal = ordered.FindIndex(t => t.Index == fileStreamIndex);
                if (ordinal < 0)
                    return;

                // Prefer language / label match (HLS EXT-X-MEDIA order can differ from file index).
                if (catalog is not null)
                {
                    for (var i = 0; i < tracks.Groups.Size(); i++)
                    {
                        var group = (Tracks.Group)tracks.Groups.Get(i)!;
                        if (group.Type != C.TrackTypeAudio)
                            continue;

                        for (var j = 0; j < group.Length; j++)
                        {
                            var format = group.GetTrackFormat(j);
                            if (format is null)
                                continue;

                            if (!string.IsNullOrEmpty(catalog.Language)
                                && string.Equals(
                                    format.Language, catalog.Language, StringComparison.OrdinalIgnoreCase))
                            {
                                SelectAudioOverride(player, group, j);
                                return;
                            }

                            if (!string.IsNullOrEmpty(catalog.Name)
                                && !string.IsNullOrEmpty(format.Label)
                                && format.Label.Contains(catalog.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                SelectAudioOverride(player, group, j);
                                return;
                            }
                        }
                    }
                }

                var audioGroupOrder = 0;
                for (var i = 0; i < tracks.Groups.Size(); i++)
                {
                    var group = (Tracks.Group)tracks.Groups.Get(i)!;
                    if (group.Type != C.TrackTypeAudio)
                        continue;
                    if (audioGroupOrder == ordinal)
                    {
                        SelectAudioOverride(player, group, 0);
                        return;
                    }

                    audioGroupOrder++;
                }
            }
        });
    }

    private static void SelectAudioOverride(IPlayer player, Tracks.Group group, int trackIndex)
    {
        var newParams = player.TrackSelectionParameters!
            .BuildUpon()!
            .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup, trackIndex))!
            .Build();
        player.TrackSelectionParameters = newParams;
        NativeVideoDebug.Log(
            "SelectAudioTrack idx=" + trackIndex + " groupLen=" + group.Length);
    }

    private void OnSwitchSubtitleTrack(string? slug)
    {
        MainThread.BeginInvokeOnMainThread(() => TrySwitchSubtitleTrack(slug, attempt: 0));
    }

    private void TrySwitchSubtitleTrack(string? slug, int attempt)
    {
        var player = GetPlayer(NativePlayer);
        if (player is null)
            return;

        if (slug is null)
        {
            var disableParams = player.TrackSelectionParameters!
                .BuildUpon()!
                .ClearOverridesOfType(C.TrackTypeText)!
                .SetTrackTypeDisabled(C.TrackTypeText, true)!
                .Build();
            player.TrackSelectionParameters = disableParams;
            NativeVideoDebug.Log("SelectTextTrack off");
            return;
        }

        var tracks = player.CurrentTracks;
        if (tracks?.Groups is null || tracks.Groups.Size() == 0)
        {
            if (attempt < 5 && NativePlayer.Handler?.PlatformView is Android.Views.View platformView)
            {
                NativeVideoDebug.Log("SelectTextTrack retry wait tracks attempt=" + attempt);
                platformView.PostDelayed(() => TrySwitchSubtitleTrack(slug, attempt + 1), 250);
            }
            else
                NativeVideoDebug.Log("SelectTextTrack abort no tracks slug=" + slug);
            return;
        }

        // Each HLS #EXT-X-MEDIA:TYPE=SUBTITLES creates its own TrackGroup with 1 track.
        var textGroupIndex = 0;
        int? targetTextGroupOrder = null;
        if (int.TryParse(slug.AsSpan(4), out var fileStreamIndex))
        {
            var subtitleTracks = _playerService.SubtitleTracks;
            for (var idx = 0; idx < subtitleTracks.Count; idx++)
            {
                if (subtitleTracks[idx].Index == fileStreamIndex)
                {
                    targetTextGroupOrder = idx;
                    break;
                }
            }
        }

        for (var i = 0; i < tracks.Groups.Size(); i++)
        {
            var group = (Tracks.Group)tracks.Groups.Get(i)!;
            if (group.Type != C.TrackTypeText)
                continue;

            for (var j = 0; j < group.Length; j++)
            {
                var format = group.GetTrackFormat(j);
                if (format?.Label == slug || format?.Id == slug)
                {
                    SelectTextTrack(player, group, j);
                    return;
                }
            }

            if (targetTextGroupOrder == textGroupIndex)
            {
                SelectTextTrack(player, group, 0);
                return;
            }

            textGroupIndex++;
        }

        if (attempt < 5 && NativePlayer.Handler?.PlatformView is Android.Views.View view)
        {
            NativeVideoDebug.Log("SelectTextTrack retry no match slug=" + slug + " attempt=" + attempt);
            view.PostDelayed(() => TrySwitchSubtitleTrack(slug, attempt + 1), 250);
        }
        else
            NativeVideoDebug.Log("SelectTextTrack miss slug=" + slug);
    }

    private static void SelectTextTrack(IPlayer player, Tracks.Group group, int trackIdx)
    {
        var newParams = player.TrackSelectionParameters!
            .BuildUpon()!
            .ClearOverridesOfType(C.TrackTypeText)!
            .SetTrackTypeDisabled(C.TrackTypeText, false)!
            .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup, trackIdx))!
            .Build();

        player.TrackSelectionParameters = newParams;
        NativeVideoDebug.Log("SelectTextTrack idx=" + trackIdx + " groupLen=" + group.Length);
    }

    private static void SetImmersiveMode(Android.App.Activity activity)
    {
        var window = activity.Window;
        if (window is null) return;

#pragma warning disable CA1422, CS0618
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.SetDecorFitsSystemWindows(false);
            var controller = window.InsetsController;
            if (controller is not null)
            {
                controller.Hide(Android.Views.WindowInsets.Type.StatusBars()
                    | Android.Views.WindowInsets.Type.NavigationBars());
                controller.SystemBarsBehavior =
                    (int)Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }

        window.SetStatusBarColor(Android.Graphics.Color.Transparent);
        window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
        window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
        window.AddFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            window.Attributes!.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
        }

        window.DecorView.SystemUiFlags =
            Android.Views.SystemUiFlags.Fullscreen
            | Android.Views.SystemUiFlags.HideNavigation
            | Android.Views.SystemUiFlags.ImmersiveSticky
            | Android.Views.SystemUiFlags.LayoutFullscreen
            | Android.Views.SystemUiFlags.LayoutHideNavigation
            | Android.Views.SystemUiFlags.LayoutStable;
#pragma warning restore CA1422, CS0618
    }

    private static void SetLandscapeOrientationPlatform()
    {
        var activity = Platform.CurrentActivity;
        if (activity is not null)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
            SetImmersiveMode(activity);
        }
    }

    private static void RestoreOrientationPlatform()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return;

        activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Unspecified;

        var window = activity.Window;
        if (window is null) return;

#pragma warning disable CA1422, CS0618
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.SetDecorFitsSystemWindows(false);
            var controller = window.InsetsController;
            controller?.Show(Android.Views.WindowInsets.Type.StatusBars()
                | Android.Views.WindowInsets.Type.NavigationBars());
        }

        window.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
        window.ClearFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            window.Attributes!.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.Default;
        }

        window.DecorView.SystemUiFlags = Android.Views.SystemUiFlags.Visible;

        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(Android.Graphics.Color.Transparent);
            window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
        }
#pragma warning restore CA1422, CS0618
    }


    private void ApplySelectedTrackOverrides()
    {
        if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
            return;

        var url = _playerService.Source.Url;
        if (_directTrackOverrideUrl == url)
            return;

        if (_playerService.SelectedAudioTrack is { } audio)
            OnSwitchAudioTrack($"audio-{audio.Index}");

        if (_playerService.SelectedSubtitleTrack is { IsTextBased: true } sub)
            OnSwitchSubtitleTrack($"sub-{sub.Index}");
        else if (_playerService.SelectedSubtitleTrack is null)
            OnSwitchSubtitleTrack(null);

        // Only lock after we actually had track groups to select against.
        var player = GetPlayer(NativePlayer);
        var tracks = player?.CurrentTracks;
        if (tracks?.Groups is not null && tracks.Groups.Size() > 0)
            _directTrackOverrideUrl = url;
    }

    private void ApplyDirectPlayTrackOverrides() => ApplySelectedTrackOverrides();
}

