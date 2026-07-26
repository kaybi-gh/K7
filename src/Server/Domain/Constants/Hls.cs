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
    /// </summary>
    public const int MinKeyframeSegmentDurationMs = 500;
}
