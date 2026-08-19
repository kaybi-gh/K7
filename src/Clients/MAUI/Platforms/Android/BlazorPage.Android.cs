using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Helpers;
using Microsoft.JSInterop;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private Android.Views.ViewTreeObserver.IOnGlobalFocusChangeListener? _videoFocusBounceListener;
    private bool _videoFocusBounceAttached;
    private int _androidHttpTimeoutRetryCount;
    private DefaultHttpDataSource.Factory? _exoHttpDataSourceFactory;
    private Dictionary<string, string>? _exoHttpRequestHeaders;

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

    internal void EnsureVideoSurfaceNotFocusable() => SuppressPlayerViewFocus();

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
        ApplyAndroidHlsAvSyncSettings(GetPlayer(NativePlayer));
        SetVideoFocusOwnership(active: true);
    }

    /// <summary>
    /// MediaElement's DefaultHttpDataSource uses 8s connect/read timeouts. K7 HLS init.m4s can
    /// take much longer while ffmpeg seeks for mid-stream resume (server waits up to ~90s).
    /// Rebind the real ExoPlayer with longer timeouts so TV/slow links do not fail at 0:00.
    /// </summary>
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
            var mediaSource = CreateAndroidStreamingMediaSource(httpFactory, mediaItem, url);
            if (mediaSource is null)
                return;

            exo.PlayWhenReady = true;
            exo.SetMediaSource(mediaSource);
            exo.Prepare();
            ApplyAndroidHlsAvSyncSettings(exo);
            _androidHttpTimeoutRetryCount = 0;

            // Do not PostDelayed SetMediaSource again: a second Prepare resets HLS to
            // startSeconds/segment boundaries (~1015s jumps), fights PendingSeek, and blinks.
            // Long timeouts are already on this bind; MediaFailed auth path rebinds explicitly.
        }
        catch (Exception)
        {
            // Best-effort rebind - toolkit default timeouts remain if this fails.
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
        NativePlayer.Play();

        if (resumeAtSeconds > 1)
            SeekNativeVideoAsync(resumeAtSeconds).FireAndForget();
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
            var mediaSource = CreateAndroidStreamingMediaSource(httpFactory, mediaItem, url);
            if (mediaSource is null)
                return;

            exo.PlayWhenReady = true;
            exo.SetMediaSource(mediaSource);
            exo.Prepare();
            ApplyAndroidHlsAvSyncSettings(exo);
        }
        catch (Exception)
        {
            // Best-effort reassert - leave the previous bind in place.
        }
    }

    partial void OnAfterNativeVideoSeek()
    {
        EnsureVideoSurfaceNotFocusable();
        // Native XAML chrome owns input; bouncing into the (hidden) WebView after seeks
        // causes focus flashes and can stall ExoPlayer on TV.
        if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible)
            return;

        if (!HasWebViewWindowFocus())
            BounceWindowFocusToWebView();
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
            var currentPos = NativePlayer.Position.TotalSeconds;
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

            // Prefer MediaElement.SeekTo after SeekParameters are set on the real ExoPlayer.
            // Direct IExoPlayerInvoker.SeekTo can exact-seek and leave TextureView frozen until
            // the next independent segment while audio advances.
            try
            {
                NativePlayer.SeekTo(TimeSpan.FromSeconds(targetSeconds));
            }
            catch (Exception)
            {
                try
                {
                    player.SeekTo((long)(targetSeconds * 1000.0));
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
            if (resumePlayback)
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

    private void SetVideoFocusOwnership(bool active)
    {
        EnsureVideoSurfaceNotFocusable();

        if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible)
        {
            // Native XAML chrome owns input - do not bounce window focus into the (hidden) WebView.
            DetachVideoFocusBounceListener();
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

        if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView webView)
            return;

        // Paint-only video: never allow PlayerView/TextureView (or anything else) to keep
        // window focus while Blazor owns the HUD. Do not touch DOM focus.
        if (newFocus is null || IsDescendantOf(webView, newFocus))
            return;


        EnsureVideoSurfaceNotFocusable();
        BounceWindowFocusToWebView();
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

    private void SuppressPlayerViewFocus()
    {
        try
        {
            var platformView = NativePlayer.Handler?.PlatformView as Android.Views.View;
            if (platformView is null)
                return;

            DisableFocusRecursive(platformView);
            var playerView = FindPlayerView(platformView);
            if (playerView is not null)
            {
                playerView.Focusable = false;
                playerView.FocusableInTouchMode = false;
                playerView.DescendantFocusability = Android.Views.DescendantFocusability.BlockDescendants;
            }
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

    private static IMediaSource? CreateAndroidStreamingMediaSource(
        DefaultHttpDataSource.Factory httpFactory,
        MediaItem mediaItem,
        string url)
    {
#pragma warning disable CS0618 // IMediaSourceFactory marked obsolete in bindings but is the Media3 API
        if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            var hlsFactory = new AndroidX.Media3.ExoPlayer.Hls.HlsMediaSource.Factory(httpFactory)!;
            return hlsFactory.CreateMediaSource(mediaItem);
        }

        var mediaSourceFactory = new DefaultMediaSourceFactory(httpFactory);
        return mediaSourceFactory.CreateMediaSource(mediaItem);
#pragma warning restore CS0618
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

    private sealed class VideoFocusBounceListener(BlazorPage page)
        : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnGlobalFocusChangeListener
    {
        public void OnGlobalFocusChanged(Android.Views.View? oldFocus, Android.Views.View? newFocus)
            => page.OnVideoGlobalFocusChanged(oldFocus, newFocus);
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
            if (player is null) return;

            var tracks = player.CurrentTracks;
            if (tracks?.Groups is null) return;

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
                        var newParams = player.TrackSelectionParameters!
                            .BuildUpon()!
                            .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup, j))!
                            .Build();

                        player.TrackSelectionParameters = newParams;
                        return;
                    }
                }
            }
        });
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

}
