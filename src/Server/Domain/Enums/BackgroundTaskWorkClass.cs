namespace K7.Server.Domain.Enums;

/// <summary>
/// Stable scheduling band of a task: what it contributes to, and at which stage of the critical path.
/// </summary>
/// <remarks>
/// Values ARE the scheduling weights, in descending order of urgency, so the scheduler can order by
/// this column directly and the ordering stays indexable without a computed column. Do not renumber
/// without a matching migration: values are persisted.
/// </remarks>
public enum BackgroundTaskWorkClass
{
    /// <summary>Seekbar thumbnails, intro detection, image variants, theme songs. Never blocks a user.</summary>
    Polish = 100,

    /// <summary>Keyframe extraction and other work that only improves an already playable media.</summary>
    Prepare = 200,

    /// <summary>Metadata refresh and main poster download: makes a visible media presentable.</summary>
    CriticalEnrich = 300,

    /// <summary>Media creation and file-to-media linking: makes a media visible and navigable.</summary>
    CriticalLink = 390,

    /// <summary>
    /// Container probe: makes a media playable. Ranks above <see cref="CriticalLink"/> on purpose, so
    /// that the probes of a scan batch drain before the media creation tasks of the same batch obtain a
    /// worker. The reverse order would let a media become visible while its probe is still queued, which
    /// is the ghost-page state this scheduling exists to avoid.
    /// </summary>
    CriticalProbe = 400
}
