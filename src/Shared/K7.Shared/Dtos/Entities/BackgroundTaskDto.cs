using K7.Server.Domain.Entities;
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

    public static BackgroundTaskDto FromDomain(BackgroundTask domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        TargetEntityType = domain.TargetEntityType,
        TargetEntityId = domain.TargetEntityId,
        Status = domain.Status,
        Priority = domain.Priority,
        RetryCount = domain.RetryCount,
        MaxRetryCount = domain.MaxRetryCount
    };
}
