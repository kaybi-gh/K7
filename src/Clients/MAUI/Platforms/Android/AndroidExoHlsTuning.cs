using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Hls;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.ExoPlayer.Upstream;
using AndroidX.Media3.UI;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// HLS MediaSource tuning on the CommunityToolkit MediaElement ExoPlayer: text-track retries
/// when the server returns 503 while VTT extract runs. LoadControl is owned by MediaElement
/// (do not replace PlayerView.Player - breaks MediaElement.SeekTo and transport).
/// </summary>
internal static class AndroidExoHlsTuning
{
    private const int TextTrackMinRetries = 12;
    private const int Text503MaxBackoffMs = 15_000;

    private static readonly K7LoadErrorHandlingPolicy SharedLoadErrorPolicy = new();

    internal static void ApplyPlaybackSurfaceTuning(IExoPlayer exo, PlayerView? playerView)
    {
        try
        {
            exo.VideoScalingMode = C.VideoScalingModeScaleToFit;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
                exo.VideoChangeFrameRateStrategy = C.VideoChangeFrameRateStrategyOff;
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

    internal static IMediaSource? CreateStreamingMediaSource(
        DefaultHttpDataSource.Factory httpFactory,
        MediaItem mediaItem,
        string url)
    {
#pragma warning disable CS0618
        if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
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
