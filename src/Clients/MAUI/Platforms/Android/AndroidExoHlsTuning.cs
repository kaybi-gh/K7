using System.Reflection;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Hls;
using AndroidX.Media3.ExoPlayer.MediaCodec;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.ExoPlayer.Upstream;
using AndroidX.Media3.UI;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Helpers;
using K7.Shared;
using Microsoft.Maui.Storage;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// HLS MediaSource tuning on the CommunityToolkit MediaElement ExoPlayer: text-track retries
/// when the server returns 503 while VTT extract runs. On Android TV, also installs a tuned
/// ExoPlayer (decoder fallback, extension renderers ON, audio offload).
/// MediaManager is pointed at that player for Play/Pause but is not an IPlayerListener.
/// HDMI tunneling stays off. Dolby Vision Profile 8 can use HEVC/HDR10 instead of
/// <c>video/dolby-vision</c> (device setting, TV default).
/// </summary>
internal static class AndroidExoHlsTuning
{
    private const int TextTrackMinRetries = 12;
    private const int Text503MaxBackoffMs = 15_000;
    // Bump tag when TV player policy changes so an old PlayerView is replaced.
    private const string TunedPlayerTagPrefix = "k7-tv-hw-player-hostexo-buf";

    private static readonly K7LoadErrorHandlingPolicy SharedLoadErrorPolicy = new();

    /// <summary>
    /// Android TV (and Amlogic boxes that report a phone/tablet idiom): tuned
    /// renderer factory. Tunneling stays off. DV Profile 8 may map to HEVC.
    /// </summary>
    internal static IExoPlayer? TryInstallTunedPlayer(
        MediaElement mediaElement,
        PlayerView? playerView)
    {
        if (playerView?.Context is null)
            return null;

        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        if (!AndroidExoPlaybackPolicy.ShouldInstallTunedExoPlayer(
                IsAndroidTelevision(),
                manufacturer,
                model))
        {
            return playerView.Player as IExoPlayer;
        }

        if (playerView.Tag?.ToString() == CurrentTunedPlayerTag(manufacturer, model)
            && playerView.Player is IExoPlayer already)
        {
            return already;
        }

        try
        {
            var context = playerView.Context;
            var bufferSize = ResolveBufferSize(manufacturer, model);
            var tunedTag = CurrentTunedPlayerTag(manufacturer, model);
            var renderers = new DefaultRenderersFactory(context)!
                .SetEnableDecoderFallback(true)!
                .SetExtensionRendererMode(DefaultRenderersFactory.ExtensionRendererModeOn)!;

#pragma warning disable CS0618
            var exoBuilder = new ExoPlayerBuilder(context)!
                .SetRenderersFactory(renderers)!
                .SetWakeMode(C.WakeModeLocal)!
                .SetHandleAudioBecomingNoisy(true)!;
            TrySetLoadControl(exoBuilder, bufferSize);
            var exo = exoBuilder.Build()!;
#pragma warning restore CS0618

            var tunneling = AndroidExoPlaybackPolicy.ShouldEnableHdmiTunneling(manufacturer, model);
            var offload = AndroidExoPlaybackPolicy.ShouldEnableAudioOffload(
                IsAndroidTelevision(),
                manufacturer,
                model);
            TryApplyPlaybackPreferences(exo, tunneling, offload);
            TryApplyMovieAudioAttributes(exo);

            var old = playerView.Player;
            if (old is IPlayerListener oldListener)
            {
                try
                {
                    old.RemoveListener(oldListener);
                }
                catch
                {
                }
            }

            // Point MediaManager at the tuned Exo so Play/Pause still reach it, but do not
            // add MediaManager as IPlayerListener. Toolkit ticks marshal to the MAUI UI
            // thread and invalidate PlayerView; a TV Exo host must not do that.
            TryBindMediaManagerPlayerWithoutListener(mediaElement, old, exo);

            playerView.Player = exo;
            playerView.Tag = new Java.Lang.String(tunedTag);

            try
            {
                old?.Release();
            }
            catch
            {
            }

            return exo;
        }
        catch
        {
            return playerView.Player as IExoPlayer;
        }
    }

    private static string CurrentTunedPlayerTag(string manufacturer, string model) =>
        TunedPlayerTagPrefix
        + ExoVideoBufferPolicy.Persist(ResolveBufferSize(manufacturer, model));

    private static ExoVideoBufferSize ResolveBufferSize(string manufacturer, string model)
    {
        var stored = ExoVideoBufferSize.Auto;
        try
        {
            stored = ExoVideoBufferPolicy.Parse(
                Preferences.Default.Get(PreferenceKeys.VIDEO_EXO_BUFFER.Name, ExoVideoBufferPolicy.Auto));
        }
        catch
        {
        }

        var television = AndroidExoPlaybackPolicy.ShouldInstallTunedExoPlayer(
            IsAndroidTelevision(),
            manufacturer,
            model);
        return ExoVideoBufferPolicy.Resolve(stored, television);
    }

    private static void TrySetLoadControl(ExoPlayerBuilder builder, ExoVideoBufferSize size)
    {
        if (size is ExoVideoBufferSize.Default or ExoVideoBufferSize.Auto)
            return;

        var minMs = size == ExoVideoBufferSize.ExtraLarge ? 100_000 : 50_000;
        var maxMs = 120_000;
        try
        {
            var loadControl = new DefaultLoadControl.Builder()!
                .SetBufferDurationsMs(minMs, maxMs, 2_500, 5_000)!
                .Build();
            builder.SetLoadControl(loadControl);
        }
        catch
        {
        }
    }
    private static void TryApplyPlaybackPreferences(IExoPlayer exo, bool tunnelingEnabled, bool audioOffload)
    {
        try
        {
            var builder = exo.TrackSelectionParameters?.BuildUpon();
            if (builder is null)
                return;

            if (builder is Java.Lang.Object javaBuilder)
            {
                TryInvokeJavaBoolean(javaBuilder, "setTunnelingEnabled", tunnelingEnabled);
                TryInvokeJavaBoolean(
                    javaBuilder,
                    "setAllowInvalidateSelectionsOnRendererCapabilitiesChange",
                    true);
            }

            var offloadMode = audioOffload
                ? TrackSelectionParameters.AudioOffloadPreferences.AudioOffloadModeEnabled
                : TrackSelectionParameters.AudioOffloadPreferences.AudioOffloadModeDisabled;
            var offload = new TrackSelectionParameters.AudioOffloadPreferences.Builder()!
                .SetAudioOffloadMode(offloadMode)!
                .Build();
            builder.SetAudioOffloadPreferences(offload);
            exo.TrackSelectionParameters = builder.Build();

            TrySetTunnelingOnDefaultTrackSelector(exo, tunnelingEnabled);
        }
        catch
        {
        }
    }

    /// <summary>
    /// USAGE_MEDIA + MOVIE, handle audio focus.
    /// </summary>
    private static void TryApplyMovieAudioAttributes(IExoPlayer exo)
    {
        try
        {
            var attrs = new AudioAttributes.Builder()!
                .SetUsage(C.UsageMedia)!
                .SetContentType(C.AudioContentTypeMovie)!
                .Build();
            exo.SetAudioAttributes(attrs, true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Amlogic HAL AFR (policy=2) retimes HDMI every few seconds (switch_name 3200/4000/...)
    /// and correlates with micro-hitches. Call after app AFR has switched. Restore "2"
    /// when leaving the player. Best-effort; may be denied without vendor perms.
    /// </summary>
    internal static void TrySetVendorVideoAfrPolicy(string value)
    {
        try
        {
            var cls = Java.Lang.Class.ForName("android.os.SystemProperties");
            var set = cls?.GetMethod(
                "set",
                Java.Lang.Class.FromType(typeof(Java.Lang.String)),
                Java.Lang.Class.FromType(typeof(Java.Lang.String)));
            if (set is null)
                return;

            _ = set.Invoke(
                null,
                [new Java.Lang.String("vendor.media.mediahal.videodec.afr.policy"), new Java.Lang.String(value)]);
        }
        catch
        {
        }
    }

    internal static bool IsAndroidTelevision()
    {
        var context = global::Android.App.Application.Context;
        var uiMode = context.Resources?.Configuration?.UiMode ?? 0;
        if ((uiMode & global::Android.Content.Res.UiMode.TypeMask)
            == global::Android.Content.Res.UiMode.TypeTelevision)
        {
            return true;
        }

        return context.PackageManager?.HasSystemFeature(
            global::Android.Content.PM.PackageManager.FeatureLeanback) == true;
    }

    private static void TryBindMediaManagerPlayerWithoutListener(
        MediaElement mediaElement,
        IPlayer? oldPlayer,
        IExoPlayer exo)
    {
        try
        {
            var handler = mediaElement.Handler;
            if (handler is null)
                return;

            var managerProp = handler.GetType().GetProperty(
                "MediaManager",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var manager = managerProp?.GetValue(handler);
            if (manager is null)
                return;

            if (manager is IPlayerListener listener)
            {
                if (oldPlayer is not null)
                {
                    try
                    {
                        oldPlayer.RemoveListener(listener);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    exo.RemoveListener(listener);
                }
                catch
                {
                }
            }

            var playerProp = manager.GetType().GetProperty(
                "Player",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (playerProp is not null && playerProp.CanWrite)
                playerProp.SetValue(manager, exo);
        }
        catch
        {
        }
    }

    internal static void ApplyPlaybackSurfaceTuning(IExoPlayer exo, PlayerView? playerView)
    {
        try
        {
            exo.VideoScalingMode = C.VideoScalingModeScaleToFit;
            var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
            var model = global::Android.OS.Build.Model ?? "";
            var afrMode = AndroidDisplayAfr.ResolveMode();
            if (OperatingSystem.IsAndroidVersionAtLeast(23)
                && !AndroidExoPlaybackPolicy.ShouldAllowSurfaceFrameRateChanges(
                    afrMode,
                    IsAndroidTelevision(),
                    manufacturer,
                    model))
            {
                exo.VideoChangeFrameRateStrategy = C.VideoChangeFrameRateStrategyOff;
            }

            var tunneling = AndroidExoPlaybackPolicy.ShouldEnableHdmiTunneling(manufacturer, model);
            var offload = AndroidExoPlaybackPolicy.ShouldEnableAudioOffload(
                IsAndroidTelevision(),
                manufacturer,
                model);
            TryApplyPlaybackPreferences(exo, tunneling, offload);
            TryApplyMovieAudioAttributes(exo);
        }
        catch
        {
        }

        if (playerView is null)
            return;

        HidePlayerViewIdleChrome(playerView);
        try
        {
            playerView.SetShutterBackgroundColor(global::Android.Graphics.Color.Transparent);
            playerView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            playerView.Background = null;
        }
        catch
        {
        }

        FlattenPlayerViewForHardwareOverlay(playerView);
        playerView.Visibility = global::Android.Views.ViewStates.Visible;

        try
        {
            var boolClass = Java.Lang.Boolean.Type;
            if (boolClass is not null)
            {
                var method = playerView.Class?.GetMethod("setKeepContentOnPlayerReset", boolClass);
                if (method is not null)
                    method.Invoke(playerView, true);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Media3 PlayerView idle/artwork is a tiny play-in-circle bitmap
    /// (<c>exo_edit_mode_logo</c>) scaled to the panel. Hide it and keep a black
    /// shutter so close/stop does not flash that placeholder. Do not apply the
    /// black fill during play: an opaque PlayerView background composites over
    /// SurfaceView on Amlogic and drops HEVC frames.
    /// </summary>
    internal static void SuppressPlayerViewPlaceholder(PlayerView? playerView)
    {
        if (playerView is null)
            return;

        HidePlayerViewIdleChrome(playerView);

        try
        {
            playerView.SetShutterBackgroundColor(global::Android.Graphics.Color.Black);
            playerView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        }
        catch
        {
        }

        FlattenPlayerViewForHardwareOverlay(playerView);
    }

    private static void HidePlayerViewIdleChrome(PlayerView playerView)
    {
        try
        {
            playerView.UseController = false;
        }
        catch
        {
        }

        try
        {
            playerView.DefaultArtwork = null;
        }
        catch
        {
        }

        TrySetBooleanProperty(playerView, "UseArtwork", false);
        TrySetIntProperty(playerView, "ArtworkDisplayMode", 0);
        TryInvokeJavaInt(playerView, "setArtworkDisplayMode", 0);
        TryInvokeJavaInt(playerView, "setShowBuffering", 0);
    }

    /// <summary>
    /// Re-hide PlayerView shutter/artwork after first frame. MediaElement layout can
    /// restore those layers; on Amlogic they blend over SurfaceView and drop HEVC frames.
    /// </summary>
    internal static void ReapplyHardwareOverlayFlatten(PlayerView? playerView)
    {
        if (playerView is null)
            return;

        FlattenPlayerViewForHardwareOverlay(playerView);
    }

    /// <summary>
    /// Opening the playback settings panel stops Amlogic drops: it is a late layout of an
    /// opaque clipped view, not a decoder reset. Replay that compositor update without the menu.
    /// </summary>
    internal static void KickSurfaceComposition(PlayerView? playerView)
    {
        if (playerView is null)
            return;

        try
        {
            playerView.RequestLayout();
            playerView.Invalidate();
            if (playerView.VideoSurfaceView is global::Android.Views.View surface)
            {
                surface.RequestLayout();
                surface.Invalidate();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// A bare SurfaceView avoids extra GPU layers. Media3 PlayerView keeps a shutter, artwork,
    /// and a full-screen SubtitleView over the video plane. On Amlogic that GPU blend
    /// drops HEVC frames even when chrome is hidden. Hide every subtree that does not
    /// contain the video surface; cues unhide SubtitleView via ExoPlaybackBridge.OnCues.
    /// </summary>
    private static void FlattenPlayerViewForHardwareOverlay(PlayerView playerView)
    {
        try
        {
            if (playerView.VideoSurfaceView is global::Android.Views.SurfaceView surface)
                surface.SetZOrderMediaOverlay(false);
        }
        catch
        {
        }

        HidePlayerViewDecor(playerView);
    }

    private static bool HidePlayerViewDecor(global::Android.Views.View? view)
    {
        if (view is null)
            return false;

        if (view is global::Android.Views.SurfaceView or global::Android.Views.TextureView)
            return true;

        if (view is SubtitleView subtitle)
        {
            subtitle.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            subtitle.SetLayerType(global::Android.Views.LayerType.None, null);
            subtitle.Visibility = global::Android.Views.ViewStates.Gone;
            return false;
        }

        if (view is global::Android.Views.ViewGroup group)
        {
            var hasVideo = false;
            for (var i = 0; i < group.ChildCount; i++)
                hasVideo |= HidePlayerViewDecor(group.GetChildAt(i));

            if (!hasVideo)
                group.Visibility = global::Android.Views.ViewStates.Gone;

            return hasVideo;
        }

        view.Visibility = global::Android.Views.ViewStates.Gone;
        return false;
    }

    private static void TrySetTunnelingOnDefaultTrackSelector(IExoPlayer exo, bool tunnelingEnabled)
    {
        try
        {
            var selector = TryGetJavaTrackSelector(exo);
            if (selector is null)
                return;

            var parameters = TryInvokeNoArg(selector, "getParameters");
            if (parameters is null)
                return;

            var builder = TryInvokeNoArg(parameters, "buildUpon");
            if (builder is not Java.Lang.Object javaBuilder)
                return;

            if (!TryInvokeJavaBoolean(javaBuilder, "setTunnelingEnabled", tunnelingEnabled))
                return;

            var built = TryInvokeNoArg(javaBuilder, "build");
            if (built is null)
                return;

            TryInvokeOneArg(selector, "setParameters", built);
        }
        catch
        {
        }
    }

    private static Java.Lang.Object? TryGetJavaTrackSelector(IExoPlayer exo)
    {
        try
        {
            var prop = exo.GetType().GetProperty("TrackSelector");
            if (prop?.GetValue(exo) is Java.Lang.Object fromProp)
                return fromProp;
        }
        catch
        {
        }

        return exo is Java.Lang.Object javaExo
            ? TryInvokeNoArg(javaExo, "getTrackSelector")
            : null;
    }

    private static Java.Lang.Object? TryInvokeNoArg(Java.Lang.Object target, string methodName)
    {
        try
        {
            for (var cls = target.Class; cls is not null; cls = cls.Superclass)
            {
                Java.Lang.Reflect.Method? method = null;
                try
                {
                    method = cls.GetMethod(methodName);
                }
                catch (Java.Lang.NoSuchMethodException)
                {
                }

                if (method is null)
                    continue;

                method.Accessible = true;
                return method.Invoke(target) as Java.Lang.Object;
            }
        }
        catch
        {
        }

        return null;
    }

    private static void TryInvokeOneArg(Java.Lang.Object target, string methodName, Java.Lang.Object arg)
    {
        try
        {
            var argClass = arg.Class;
            if (argClass is null)
                return;

            for (var cls = target.Class; cls is not null; cls = cls.Superclass)
            {
                Java.Lang.Reflect.Method? method = null;
                try
                {
                    method = cls.GetMethod(methodName, argClass);
                }
                catch (Java.Lang.NoSuchMethodException)
                {
                }

                if (method is null)
                    continue;

                method.Accessible = true;
                method.Invoke(target, arg);
                return;
            }
        }
        catch
        {
        }
    }

    private static bool TryInvokeJavaBoolean(Java.Lang.Object target, string methodName, bool value)
    {
        try
        {
            var boolClass = Java.Lang.Boolean.Type;
            if (boolClass is null)
                return false;

            for (var cls = target.Class; cls is not null; cls = cls.Superclass)
            {
                Java.Lang.Reflect.Method? method = null;
                try
                {
                    method = cls.GetMethod(methodName, boolClass);
                }
                catch (Java.Lang.NoSuchMethodException)
                {
                }

                if (method is null)
                    continue;

                method.Accessible = true;
                method.Invoke(target, value);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static void TrySetBooleanProperty(object target, string name, bool value)
    {
        try
        {
            var prop = target.GetType().GetProperty(name);
            if (prop is not null && prop.CanWrite)
                prop.SetValue(target, value);
        }
        catch
        {
        }
    }

    private static void TrySetIntProperty(object target, string name, int value)
    {
        try
        {
            var prop = target.GetType().GetProperty(name);
            if (prop is not null && prop.CanWrite)
                prop.SetValue(target, value);
        }
        catch
        {
        }
    }

    private static void TryInvokeJavaInt(Java.Lang.Object target, string methodName, int value)
    {
        try
        {
            var intClass = Java.Lang.Integer.Type;
            if (intClass is null)
                return;

            for (var cls = target.Class; cls is not null; cls = cls.Superclass)
            {
                Java.Lang.Reflect.Method? method = null;
                try
                {
                    method = cls.GetMethod(methodName, intClass);
                }
                catch (Java.Lang.NoSuchMethodException)
                {
                }

                if (method is null)
                    continue;

                method.Accessible = true;
                method.Invoke(target, value);
                return;
            }
        }
        catch
        {
        }
    }

    internal static IMediaSource? CreateStreamingMediaSource(
        DefaultHttpDataSource.Factory httpFactory,
        MediaItem mediaItem,
        string url)
    {
#pragma warning disable CS0618
        if (StreamingSourceKind.IsHls(mimeType: null, url))
        {
            var hlsFactory = new HlsMediaSource.Factory(httpFactory)!
                .SetLoadErrorHandlingPolicy(SharedLoadErrorPolicy)!;
            return hlsFactory.CreateMediaSource(mediaItem);
        }

        var mediaSourceFactory = new DefaultMediaSourceFactory(httpFactory)!
            .SetLoadErrorHandlingPolicy(SharedLoadErrorPolicy)!;
        return mediaSourceFactory.CreateMediaSource(mediaItem);
#pragma warning restore CS0618
    }

    private sealed class K7LoadErrorHandlingPolicy : DefaultLoadErrorHandlingPolicy
    {
        public override int GetMinimumLoadableRetryCount(int dataType)
        {
            if (dataType == C.DataTypeMedia || dataType == C.DataTypeMediaInitialization)
                return TextTrackMinRetries;

            return base.GetMinimumLoadableRetryCount(dataType);
        }

        public override long GetRetryDelayMsFor(LoadErrorHandlingPolicyLoadErrorInfo? loadErrorInfo)
        {
            if (loadErrorInfo is null || !IsVttLoad(loadErrorInfo))
                return base.GetRetryDelayMsFor(loadErrorInfo);

            var responseCode = TryGetHttpResponseCode(loadErrorInfo.Exception);
            var attempt = Math.Max(1, loadErrorInfo.ErrorCount);
            if (responseCode == 503)
            {
                var exponent = Math.Min(attempt - 1, 4);
                return Math.Min(500L * (1L << exponent), Text503MaxBackoffMs);
            }

            return Math.Min(250L * attempt, 5_000L);
        }

        private static bool IsVttLoad(LoadErrorHandlingPolicyLoadErrorInfo loadErrorInfo)
        {
            var uri = TryGetLoadUri(loadErrorInfo.Exception);
            return uri is not null
                && uri.Contains(".vtt", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetLoadUri(Java.IO.IOException? exception)
        {
            if (exception is null)
                return null;

            Java.Lang.Throwable? cause = exception;
            for (var depth = 0; depth < 8 && cause is not null; depth++)
            {
                if (cause is HttpDataSourceInvalidResponseCodeException invalidResponse)
                    return invalidResponse.DataSpec?.Uri?.ToString();

                if (cause is HttpDataSourceHttpDataSourceException httpEx)
                    return httpEx.DataSpec?.Uri?.ToString();

                cause = cause.Cause;
            }

            return null;
        }

        private static int TryGetHttpResponseCode(Java.IO.IOException? exception)
        {
            if (exception is null)
                return 0;

            Java.Lang.Throwable? cause = exception;
            for (var depth = 0; depth < 8 && cause is not null; depth++)
            {
                if (cause is HttpDataSourceInvalidResponseCodeException invalidResponse)
                    return invalidResponse.ResponseCode;

                cause = cause.Cause;
            }

            return 0;
        }
    }

}
