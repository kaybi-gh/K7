namespace K7.Server.Domain.Entities;

public class BackgroundTask : BaseAuditableEntity
{
    public required string Name { get; set; }

    public required string RequestType { get; set; }
    public required string RequestData { get; set; }

    public string? TargetEntityType { get; set; }
    public Guid? TargetEntityId { get; set; }

    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;
    public BackgroundTaskPriority Priority { get; set; } = BackgroundTaskPriority.Lowest;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? NextRetryAfter { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}
