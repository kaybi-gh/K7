namespace K7.Server.Domain.Enums;

/// <summary>
/// Local resource a background task competes for. Lanes are the unit of configurable concurrency.
/// </summary>
/// <remarks>
/// A lane answers "which local resource does this saturate", never "how urgent is this" - urgency is
/// carried by <see cref="BackgroundTaskWorkClass"/> and the schedule score. Two tasks share a lane
/// when they contend for the same resource, so a lane limit is a meaningful operator control.
/// </remarks>
public enum BackgroundTaskLane
{
    /// <summary>Filesystem indexing.</summary>
    LibraryScan = 0,

    /// <summary>
    /// Container probing with ffprobe: file metadata and chapter extraction. Reads stream headers
    /// only, so it is IO seek bound and safe to parallelize.
    /// </summary>
    Probe = 1,

    /// <summary>Keyframe extraction for HLS transmuxing. Scans every packet, far heavier than a probe.</summary>
    FfmpegPrepare = 2,

    /// <summary>Intro/outro detection and audio analysis.</summary>
    MediaAnalysis = 3,

    /// <summary>Seekbar thumbnails and stills extracted with ffmpeg.</summary>
    ImageExtract = 4,

    /// <summary>Local image variant generation.</summary>
    ImageProcessing = 5,

    /// <summary>
    /// Media identification, metadata refresh and provider downloads. HTTP pacing stays owned by the
    /// outbound rate limiter, so this limit is about task parallelism only.
    /// </summary>
    Metadata = 6,

    /// <summary>Peer synchronization. Isolated per peer through the federation peer id.</summary>
    Federation = 7,

    /// <summary>Transcoding for offline downloads.</summary>
    DownloadTranscode = 8
}
