namespace K7.Server.Domain.Entities;

public class BackgroundTask : BaseAuditableEntity
{
    public required string Name { get; set; }

    public required string RequestType { get; set; }
    public required string RequestData { get; set; }

    public string? TargetEntityType { get; set; }
    public Guid? TargetEntityId { get; set; }

    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;

    /// <summary>Local resource this task competes for. Unit of configurable concurrency.</summary>
    public BackgroundTaskLane Lane { get; set; } = BackgroundTaskLane.Metadata;

    /// <summary>Stable scheduling band. Primary ordering key, highest first.</summary>
    public BackgroundTaskWorkClass WorkClass { get; set; } = BackgroundTaskWorkClass.Polish;

    /// <summary>
    /// Dynamic urgency, second ordering key. Raised when a user asks for a media on demand, so that
    /// what is being looked at overtakes a large indexing backlog.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Provenance, for observability. Never ordered on directly.</summary>
    public BackgroundTaskTriggeredBy TriggeredBy { get; set; } = BackgroundTaskTriggeredBy.System;

    /// <summary>
    /// Peer this task belongs to, for <see cref="BackgroundTaskLane.Federation"/>. Isolates peers from
    /// each other without polluting the fixed lane set with one group per peer.
    /// </summary>
    public Guid? FederationPeerId { get; set; }

    /// <summary>
    /// Metadata provider this Metadata-lane task competes for (tmdb, tvdb, ...). Null for non-Metadata
    /// lanes. Used as the admission sub-key so each provider runs at most one task at a time.
    /// </summary>
    public string? MetadataProviderName { get; set; }

    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 1;

    /// <summary>
    /// Number of times this task was reclaimed after its owning worker disappeared. Counted so a task
    /// that kills the process cannot loop forever without ever reaching <see cref="MaxAttempts"/>.
    /// </summary>
    public int ReclaimCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? NextRetryAfter { get; set; }
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Set when an operator asked for cancellation of a task that is already running.</summary>
    public bool CancellationRequested { get; set; }

    public string? ErrorDetails { get; set; }
}
