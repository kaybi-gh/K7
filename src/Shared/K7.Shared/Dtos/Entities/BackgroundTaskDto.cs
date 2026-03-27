using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record BackgroundTaskDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? TargetEntityType { get; init; }
    public Guid? TargetEntityId { get; init; }
    public BackgroundTaskStatus Status { get; init; }
    public BackgroundTaskPriority Priority { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetryCount { get; init; }
}
