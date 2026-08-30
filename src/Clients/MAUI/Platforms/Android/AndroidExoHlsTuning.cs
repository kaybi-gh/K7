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

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// HLS MediaSource tuning on the CommunityToolkit MediaElement ExoPlayer: text-track retries
/// when the server returns 503 while VTT extract runs. On Amlogic, also installs a tuned
/// ExoPlayer with tunneling ON (HDMI present fences) and vendor HW codec preference.
/// Dolby Vision MIME stays <c>video/dolby-vision</c> so the TV can show its HDR / DV banner;
/// earlier freezes came from toggling tunneling off after enabling it, not from HDR itself.
/// </summary>
internal static class AndroidExoHlsTuning
{
    private const int TextTrackMinRetries = 12;
    private const int Text503MaxBackoffMs = 15_000;
    // Bump tag when Amlogic player policy changes so an old PlayerView is replaced.
    private const string AmlogicTunedPlayerTag = "k7-amlogic-hdr";

    private static readonly K7LoadErrorHandlingPolicy SharedLoadErrorPolicy = new();

    /// <summary>
    /// Nokia / Amlogic: keep Media3 tunneling enabled and prefer vendor HW codecs.
    /// Do not remap Dolby Vision to HEVC - that dropped HDMI HDR InfoFrames (no TV banner)
    /// while the freezes were fixed by stable tunneling, not by SDR fallback.
    /// </summary>
    internal static IExoPlayer? TryInstallAmlogicTunedPlayer(
        MediaElement mediaElement,
        PlayerView? playerView)
    {
        if (playerView?.Context is null)
            return null;

        if (!IsAmlogicTvBox())
            return playerView.Player as IExoPlayer;

        if (playerView.Tag?.ToString() == AmlogicTunedPlayerTag
            && playerView.Player is IExoPlayer already)
        {
            return already;
        }

        try
        {
            var context = playerView.Context;
            var renderers = new DefaultRenderersFactory(context)!
                .SetEnableDecoderFallback(true)!
                .SetMediaCodecSelector(new PreferAmlogicHardwareSelector())!;

#pragma warning disable CS0618
            var exo = new ExoPlayerBuilder(context)!
                .SetRenderersFactory(renderers)!
                .SetWakeMode(C.WakeModeLocal)!
                .SetHandleAudioBecomingNoisy(true)!
                .Build()!;
#pragma warning restore CS0618

            // Tunneling helps HDMI present fences + HDR InfoFrames; keep audio offload off.
            // (Generic TryDisableTunnelingAndAudioOffload disables both - wrong for this box.)
            TryApplyAmlogicPlaybackPreferences(exo);
            TryDisableVendorVideoAfr();

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

            // MediaManager is also the toolkit IPlayerListener - move it onto the new player.
            TryRetargetMediaManagerPlayer(mediaElement, old, exo);

            playerView.Player = exo;
            playerView.Tag = new Java.Lang.String(AmlogicTunedPlayerTag);

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

    private static void TryApplyAmlogicPlaybackPreferences(IExoPlayer exo)
    {
        try
        {
            if (exo is Java.Lang.Object javaPlayer)
                TryInvokeJavaBoolean(javaPlayer, "experimentalSetOffloadSchedulingEnabled", false);

            var builder = exo.TrackSelectionParameters?.BuildUpon();
            if (builder is null)
                return;

            if (builder is Java.Lang.Object javaBuilder)
                TryInvokeJavaBoolean(javaBuilder, "setTunnelingEnabled", true);

            var offload = new TrackSelectionParameters.AudioOffloadPreferences.Builder()!
                .SetAudioOffloadMode(0)!
                .Build();
            builder.SetAudioOffloadPreferences(offload);
            exo.TrackSelectionParameters = builder.Build();
        }
        catch
        {
        }
    }

    /// <summary>
    /// Amlogic HAL AFR (policy=2) retimes HDMI every few seconds (switch_name 3200/4000/...)
    /// and correlates with micro-hitches. Best-effort; may be denied without vendor perms.
    /// </summary>
    private static void TryDisableVendorVideoAfr()
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
                [new Java.Lang.String("vendor.media.mediahal.videodec.afr.policy"), new Java.Lang.String("0")]);
        }
        catch
        {
        }
    }

    private static bool IsAmlogicTvBox()
    {
        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        // Amlogic boxes (Nokia Streaming Box, SEI Robotics, etc.).
        return manufacturer.Contains("SEI", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Contains("Amlogic", StringComparison.OrdinalIgnoreCase)
            || model.Contains("Streaming Box", StringComparison.OrdinalIgnoreCase)
            || model.Contains("Amlogic", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryRetargetMediaManagerPlayer(
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

            if (oldPlayer is not null && manager is IPlayerListener listener)
            {
                try
                {
                    oldPlayer.RemoveListener(listener);
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

            if (manager is IPlayerListener managerListener)
                exo.AddListener(managerListener);
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
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
                exo.VideoChangeFrameRateStrategy = C.VideoChangeFrameRateStrategyOff;

            // Amlogic tuned player wants tunneling ON (present fences + HDR). Do not undo it here.
            // Other devices: Media3 tunneling + offload can freeze the last video frame.
            if (IsAmlogicTvBox())
                TryApplyAmlogicPlaybackPreferences(exo);
            else
                TryDisableTunnelingAndAudioOffload(exo);
        }
        catch
        {
        }

        if (playerView is null)
            return;

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

    internal static void TryDisableTunnelingAndAudioOffload(IExoPlayer exo)
    {
        if (exo is Java.Lang.Object javaPlayer)
            TryInvokeJavaBoolean(javaPlayer, "experimentalSetOffloadSchedulingEnabled", false);

        try
        {
            var builder = exo.TrackSelectionParameters?.BuildUpon();
            if (builder is null)
                return;

            if (builder is Java.Lang.Object javaBuilder)
                TryInvokeJavaBoolean(javaBuilder, "setTunnelingEnabled", false);

            // AudioOffloadMode 0 = disabled in Media3 bindings.
            var offload = new TrackSelectionParameters.AudioOffloadPreferences.Builder()!
                .SetAudioOffloadMode(0)!
                .Build();
            builder.SetAudioOffloadPreferences(offload);
            exo.TrackSelectionParameters = builder.Build();
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

    /// <summary>
    /// Prefer vendor HW decoders (c2.amlogic.*) for the requested MIME, including
    /// <c>video/dolby-vision</c>, so HDMI can advertise HDR / Dolby Vision to the TV.
    /// </summary>
    private sealed class PreferAmlogicHardwareSelector : Java.Lang.Object, IMediaCodecSelector
    {
        public IList<MediaCodecInfo>? GetDecoderInfos(
            string? mimeType,
            bool requiresSecureDecoder,
            bool requiresTunnelingDecoder)
        {
            if (string.IsNullOrEmpty(mimeType))
                return new List<MediaCodecInfo>();

            // MediaCodecUtil.GetDecoderInfos(String, secure, tunneling) - do not depend on
            // MediaCodecSelector.DEFAULT (missing from Xamarin Media3 1.8 bindings).
            var infos = MediaCodecUtil.GetDecoderInfos(
                mimeType,
                requiresSecureDecoder,
                requiresTunnelingDecoder);
            if (infos is null || infos.Count == 0)
                return infos;

            // Prefer vendor HW (c2.amlogic.*) over Google software codecs.
            var preferred = new List<MediaCodecInfo>(infos.Count);
            var rest = new List<MediaCodecInfo>(infos.Count);
            foreach (var info in infos)
            {
                var name = info.Name ?? "";
                if (name.Contains("amlogic", StringComparison.OrdinalIgnoreCase)
                    || (name.StartsWith("OMX.", StringComparison.Ordinal)
                        && !name.Contains("google", StringComparison.OrdinalIgnoreCase)))
                {
                    preferred.Add(info);
                }
                else if (!name.Contains("android.hevc", StringComparison.OrdinalIgnoreCase)
                         && !name.Contains("google", StringComparison.OrdinalIgnoreCase))
                {
                    preferred.Add(info);
                }
                else
                {
                    rest.Add(info);
                }
            }

            preferred.AddRange(rest);
            return preferred;
        }
    }
}
