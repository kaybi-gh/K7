using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Analytics;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using System.Globalization;
using System.Reflection;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Live ExoPlayer / HDMI snapshot for the admin playback HUD.
/// </summary>
internal static class AndroidExoPlaybackStats
{
    private const int HeartbeatIntervalMs = 2_000;
    private static readonly ExoStatsAnalyticsListener Analytics = new();
    private static readonly global::Android.OS.Handler Heartbeat = new(global::Android.OS.Looper.MainLooper!);
    private static readonly HeartbeatRunnable HeartbeatTick = new();
    private static bool _heartbeatArmed;

    internal static IExoPlayer? Player { get; private set; }

    internal static void Attach(IExoPlayer exo)
    {
        if (ReferenceEquals(Player, exo))
            return;

        Detach();
        Player = exo;
        Analytics.Reset();
        TryAddAnalyticsListener(exo, Analytics);
        StartHeartbeat();
    }

    internal static string FormatCountersLine()
    {
        var exo = Player;
        if (exo is null)
            return "exo=null";

        try
        {
            TryReadDecoderCounters(exo, video: true, out var dropped, out var rendered, out var skipped);
            return "drop=" + dropped
                + " draw=" + rendered
                + " skip=" + skipped
                + " analyticsDrop=" + Analytics.DroppedSinceAttach;
        }
        catch (Exception ex)
        {
            return "counters fail " + ex.GetType().Name;
        }
    }

    internal static void Detach()
    {
        StopHeartbeat();
        var exo = Player;
        Player = null;
        if (exo is null)
            return;

        TryRemoveAnalyticsListener(exo, Analytics);
    }

    private static void StartHeartbeat()
    {
        _heartbeatArmed = true;
        Heartbeat.RemoveCallbacks(HeartbeatTick);
        Heartbeat.Post(HeartbeatTick);
    }

    private static void StopHeartbeat()
    {
        _heartbeatArmed = false;
        Heartbeat.RemoveCallbacks(HeartbeatTick);
    }

    private static void TryLogHeartbeat()
    {
        var exo = Player;
        if (exo is null)
            return;

        try
        {
            if (!exo.IsPlaying)
                return;

            TryReadDecoderCounters(exo, video: true, out var dropped, out var rendered, out var skipped);
            NativeVideoDebug.Log(
                "drop=" + dropped
                + " draw=" + rendered
                + " skip=" + skipped
                + " " + BuildPolicyLine());
        }
        catch
        {
        }
    }

    private sealed class HeartbeatRunnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        public void Run()
        {
            if (!_heartbeatArmed)
                return;

            TryLogHeartbeat();
            Heartbeat.PostDelayed(this, HeartbeatIntervalMs);
        }
    }

    internal static NativePlaybackStatsSnapshot Capture(IPlayerService player)
    {
        var url = player.Source?.Url;
        var mime = player.Source?.MimeType;
        var quality = player.SelectedQuality;
        var snapshot = new NativePlaybackStatsSnapshot
        {
            PlayMethod = NativePlaybackStatsFormatting.PlayMethod(
                url,
                mime,
                quality?.IsOriginal ?? !StreamingSourceKind.IsHls(mime, url)),
            Quality = quality?.Label ?? "",
            Buffer = NativePlaybackStatsFormatting.FormatBuffer(player.BufferedTime),
            Policy = BuildPolicyLine()
        };

        var exo = Player;
        if (exo is null)
            return WithHdmi(snapshot, player, exoFps: 0);

        try
        {
            return CaptureFromExo(exo, snapshot, player);
        }
        catch
        {
            return WithHdmi(snapshot, player, exoFps: 0);
        }
    }

    private static NativePlaybackStatsSnapshot CaptureFromExo(
        IExoPlayer exo,
        NativePlaybackStatsSnapshot baseline,
        IPlayerService player)
    {
        var video = exo.VideoFormat;
        var audio = exo.AudioFormat;
        var fps = player.Source?.SourceFrameRate ?? 0f;
        if (fps <= 1f)
            fps = TryReadFrameRate(exo);
        ReadHdmi(fps, out var hdmi, out var hdmiModes, out var cadence, out var cadenceWarning);

        var dropped = 0;
        var rendered = 0;
        var skipped = 0;
        TryReadDecoderCounters(exo, video: true, out dropped, out rendered, out skipped);

        var bufferSeconds = baseline.Buffer;
        try
        {
            var bufferedMs = exo.TotalBufferedDuration;
            if (bufferedMs >= 0 && bufferedMs < 86_400_000)
                bufferSeconds = NativePlaybackStatsFormatting.FormatBuffer(bufferedMs / 1000.0);
        }
        catch
        {
        }

        var bandwidth = Analytics.LastBandwidthBps > 0
            ? NativePlaybackStatsFormatting.FormatBitrate(Analytics.LastBandwidthBps)
            : "";

        return new NativePlaybackStatsSnapshot
        {
            PlayMethod = baseline.PlayMethod,
            Quality = baseline.Quality,
            Video = FormatVideo(video, fps),
            Audio = FormatAudio(audio),
            VideoDecoder = Analytics.VideoDecoderName ?? "",
            AudioDecoder = Analytics.AudioDecoderName ?? "",
            Hdmi = hdmi,
            HdmiModes = hdmiModes,
            Cadence = cadence,
            CadenceWarning = cadenceWarning,
            Frames = NativePlaybackStatsFormatting.FormatFrames(
                dropped > 0 ? dropped : Analytics.DroppedSinceAttach,
                rendered,
                skipped),
            Buffer = bufferSeconds,
            Bandwidth = bandwidth,
            Policy = baseline.Policy
        };
    }

    private static NativePlaybackStatsSnapshot WithHdmi(
        NativePlaybackStatsSnapshot baseline,
        IPlayerService player,
        float exoFps)
    {
        var fps = player.Source?.SourceFrameRate ?? 0f;
        if (fps <= 1f)
            fps = exoFps;
        ReadHdmi(fps, out var hdmi, out var hdmiModes, out var cadence, out var cadenceWarning);
        return new NativePlaybackStatsSnapshot
        {
            PlayMethod = baseline.PlayMethod,
            Quality = baseline.Quality,
            Video = baseline.Video,
            Audio = baseline.Audio,
            VideoDecoder = baseline.VideoDecoder,
            AudioDecoder = baseline.AudioDecoder,
            Hdmi = hdmi,
            HdmiModes = hdmiModes,
            Cadence = cadence,
            CadenceWarning = cadenceWarning,
            Frames = baseline.Frames,
            Buffer = baseline.Buffer,
            Bandwidth = baseline.Bandwidth,
            Policy = BuildPolicyLine()
        };
    }

    private static void ReadHdmi(
        float fps,
        out string hdmi,
        out string hdmiModes,
        out string cadence,
        out bool cadenceWarning)
    {
        hdmi = "";
        cadence = "";
        cadenceWarning = false;
        hdmiModes = NativePlaybackStatsFormatting.FormatHdmiModes(AndroidDisplayAfr.ListModes());
        if (!AndroidDisplayAfr.TryReadCurrentMode(out var hdmiW, out var hdmiH, out var hz))
            return;

        hdmi = "HDMI " + hdmiW + "x" + hdmiH + " @ "
            + hz.ToString("0.##", CultureInfo.InvariantCulture) + " Hz";
        var kind = AndroidHdmiFrameRateMatching.ClassifyCadence(fps, hz);
        cadence = fps <= 1f
            ? "no fps"
            : AndroidHdmiFrameRateMatching.DescribeCadence(kind);
        cadenceWarning = fps > 1f && AndroidHdmiFrameRateMatching.IsCadenceWarning(kind);
    }

    private static string BuildPolicyLine()
    {
        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        var tunnel = AndroidExoPlaybackPolicy.ShouldEnableHdmiTunneling(manufacturer, model)
            ? "tunnel on"
            : "tunnel off";
        var offload = AndroidExoPlaybackPolicy.ShouldEnableAudioOffload(
            AndroidExoHlsTuning.IsAndroidTelevision(),
            manufacturer,
            model)
            ? "offload on"
            : "offload off";
        var afr = AndroidDisplayAfr.PolicyHudLabel();
        var dv = AndroidExoHlsTuning.DolbyVisionHudLabel();
        var sfr = AndroidExoPlaybackPolicy.ShouldAllowSurfaceFrameRateChanges(
            AndroidDisplayAfr.ResolveMode(),
            AndroidExoHlsTuning.IsAndroidTelevision(),
            manufacturer,
            model)
            ? "sfr on"
            : "sfr off";
        return tunnel + "  " + offload + "  " + afr + "  " + dv + "  " + sfr
            + "  " + AndroidExoHlsTuning.VideoHostHudLabel()
            + "  " + AndroidExoHlsTuning.BufferHudLabel();
    }

    internal static float TryReadFrameRate(IExoPlayer exo)
    {
        try
        {
            var videoFormat = exo.VideoFormat;
            if (videoFormat is not null && videoFormat.FrameRate > 1f && videoFormat.FrameRate < 125f)
                return videoFormat.FrameRate;
        }
        catch
        {
        }

        try
        {
            var tracks = exo.CurrentTracks;
            if (tracks?.Groups is null)
                return 0;

            for (var i = 0; i < tracks.Groups.Size(); i++)
            {
                var group = (Tracks.Group)tracks.Groups.Get(i)!;
                if (group.Type != C.TrackTypeVideo)
                    continue;

                for (var j = 0; j < group.Length; j++)
                {
                    if (!group.IsTrackSelected(j))
                        continue;

                    var rate = group.GetTrackFormat(j)?.FrameRate ?? 0;
                    if (rate > 1f && rate < 125f)
                        return rate;
                }
            }
        }
        catch
        {
        }

        return 0;
    }

    private static string FormatVideo(Format? format, float fps)
    {
        if (format is null)
            return "";

        var codec = FirstNonEmpty(format.Codecs, format.SampleMimeType);
        var hdr = DescribeHdr(format);
        var line = NativePlaybackStatsFormatting.VideoLine(
            codec,
            format.Width,
            format.Height,
            fps,
            format.Bitrate);
        return string.IsNullOrEmpty(hdr) ? line : line + "  " + hdr;
    }

    private static string FormatAudio(Format? format)
    {
        if (format is null)
            return "";

        var codec = FirstNonEmpty(format.Codecs, format.SampleMimeType);
        return NativePlaybackStatsFormatting.AudioLine(
            codec,
            format.ChannelCount,
            format.SampleRate,
            format.Bitrate);
    }

    private static string DescribeHdr(Format format)
    {
        try
        {
            var mime = format.SampleMimeType ?? "";
            if (mime.Contains("dolby-vision", StringComparison.OrdinalIgnoreCase))
                return "DV";

            var color = format.ColorInfo;
            if (color is null)
                return "";

            var transfer = color.ColorTransfer;
            if (transfer == C.ColorTransferSt2084)
                return "HDR10";
            if (transfer == C.ColorTransferHlg)
                return "HLG";
        }
        catch
        {
        }

        return "";
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;

    private static void TryReadDecoderCounters(
        IExoPlayer exo,
        bool video,
        out int dropped,
        out int rendered,
        out int skipped)
    {
        dropped = 0;
        rendered = 0;
        skipped = 0;
        try
        {
            var propertyName = video ? "VideoDecoderCounters" : "AudioDecoderCounters";
            var counters = exo.GetType().GetProperty(propertyName)?.GetValue(exo);
            if (counters is null)
                return;

            counters.GetType().GetMethod("EnsureUpdated")?.Invoke(counters, null);
            dropped = ReadInt(counters, "DroppedBufferCount");
            rendered = ReadInt(counters, "RenderedOutputBufferCount");
            skipped = ReadInt(counters, "SkippedOutputBufferCount");
        }
        catch
        {
        }
    }

    private static int ReadInt(object counters, string name)
    {
        var value = counters.GetType().GetProperty(name)?.GetValue(counters)
            ?? counters.GetType().GetField(name)?.GetValue(counters);
        return value is int i ? i : 0;
    }

    private static void TryAddAnalyticsListener(IExoPlayer exo, IAnalyticsListener listener)
    {
        try
        {
            exo.AddAnalyticsListener(listener);
        }
        catch
        {
            TryInvokeListener(exo, "AddAnalyticsListener", listener);
        }
    }

    private static void TryRemoveAnalyticsListener(IExoPlayer exo, IAnalyticsListener listener)
    {
        try
        {
            exo.RemoveAnalyticsListener(listener);
        }
        catch
        {
            TryInvokeListener(exo, "RemoveAnalyticsListener", listener);
        }
    }

    private static void TryInvokeListener(IExoPlayer exo, string methodName, IAnalyticsListener listener)
    {
        try
        {
            exo.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(exo, [listener]);
        }
        catch
        {
        }
    }

    private sealed class ExoStatsAnalyticsListener : Java.Lang.Object, IAnalyticsListener
    {
        public string? VideoDecoderName { get; private set; }
        public string? AudioDecoderName { get; private set; }
        public int DroppedSinceAttach { get; private set; }
        public int LastBandwidthBps { get; private set; }

        public void Reset()
        {
            VideoDecoderName = null;
            AudioDecoderName = null;
            DroppedSinceAttach = 0;
            LastBandwidthBps = 0;
        }

        public void OnVideoDecoderInitialized(
            AnalyticsListenerEventTime? eventTime,
            string? decoderName,
            long initializedTimestampMs,
            long initializationDurationMs)
        {
            _ = eventTime;
            _ = initializedTimestampMs;
            _ = initializationDurationMs;
            if (!string.IsNullOrWhiteSpace(decoderName))
                VideoDecoderName = decoderName;
        }

        public void OnAudioDecoderInitialized(
            AnalyticsListenerEventTime? eventTime,
            string? decoderName,
            long initializedTimestampMs,
            long initializationDurationMs)
        {
            _ = eventTime;
            _ = initializedTimestampMs;
            _ = initializationDurationMs;
            if (!string.IsNullOrWhiteSpace(decoderName))
                AudioDecoderName = decoderName;
        }

        public void OnDroppedVideoFrames(
            AnalyticsListenerEventTime? eventTime,
            int droppedFrames,
            long elapsedMs)
        {
            _ = eventTime;
            _ = elapsedMs;
            if (droppedFrames > 0)
                DroppedSinceAttach += droppedFrames;
        }

        public void OnBandwidthEstimate(
            AnalyticsListenerEventTime? eventTime,
            int totalLoadTimeMs,
            long totalBytesLoaded,
            long bitrateEstimate)
        {
            _ = eventTime;
            _ = totalLoadTimeMs;
            _ = totalBytesLoaded;
            if (bitrateEstimate > 0 && bitrateEstimate < int.MaxValue)
                LastBandwidthBps = (int)bitrateEstimate;
        }
    }
}
