namespace K7.Server.Domain.Constants;

public static class Hls
{
    /// <summary>
    /// Equal-length grid used for pure-audio files and as a fallback when keyframe metadata
    /// is missing. Video (and demuxed A/V) streams use one segment per source keyframe instead.
    /// </summary>
    public const int TargetSegmentDurationMs = 6000;

    public const double TargetSegmentDurationSeconds = TargetSegmentDurationMs / 1000.0;

    /// <summary>
    /// Collapse pathological sub-GOP keyframe bursts when building the every-keyframe timeline.
    /// Sub-second segments make ExoPlayer snap A/V at every GOP on demuxed HLS.
    /// </summary>
    public const int MinKeyframeSegmentDurationMs = 1000;

    /// <summary>
    /// Rebase copy fMP4 tfdt onto the playlist start only when the fragment is on a
    /// different window (lazy ffmpeg reset). Do not micro-rebase audio or video copy.
    /// Source PTS (including the ~83ms video CTS) is the A/V reference for remux.
    /// </summary>
    public const int TfdtWindowResetThresholdMs = 1000;

    /// <summary>
    /// Encode creates a new timeline: snap video tfdt to the playlist. A 200-800ms
    /// hardware-encoder delay is under <see cref="TfdtWindowResetThresholdMs"/> and
    /// would otherwise stay as a constant late-video offset on ExoPlayer. Encode
    /// serve also subtracts the first-sample CTS so presentation matches #EXTINF.
    /// </summary>
    public const int VideoTfdtAlignToleranceMs = 20;

    /// <summary>
    /// Video remux keeps source composition (1s window). Video encode aligns to
    /// <see cref="VideoTfdtAlignToleranceMs"/>.
    /// </summary>
    public static int VideoTfdtRebaseToleranceMs(bool isEncode) =>
        isEncode ? VideoTfdtAlignToleranceMs : TfdtWindowResetThresholdMs;

    /// <summary>
    /// ffmpeg -segment_time_delta for keyframe cuts.
    /// </summary>
    public const double SegmentTimeDeltaSeconds = 0.05;

    /// <summary>
    /// Directory name under each indexed-file transcode folder for extracted WebVTT sidecar files.
    /// </summary>
    public const string SubtitlesCacheDirectoryName = "subtitles";
}
