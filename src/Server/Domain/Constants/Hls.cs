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
    /// Rebase fMP4 tfdt onto the playlist start only when the fragment is on a different
    /// window (lazy ffmpeg -start_at_zero reset, or a stale equal-length grid). Smaller
    /// deltas are AAC/keyframe composition error: shifting A and V independently to the
    /// playlist would create a constant lip-sync offset on every client.
    /// </summary>
    public const int TfdtWindowResetThresholdMs = 1000;

    /// <summary>
    /// ffmpeg -segment_time_delta for keyframe cuts.
    /// </summary>
    public const double SegmentTimeDeltaSeconds = 0.05;

    /// <summary>
    /// Directory name under each indexed-file transcode folder for extracted WebVTT sidecar files.
    /// </summary>
    public const string SubtitlesCacheDirectoryName = "subtitles";
}
