using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class BackgroundTaskMappings
{
    extension(BackgroundTask domain)
    {
        public BackgroundTaskDto ToBackgroundTaskDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            TargetEntityType = domain.TargetEntityType,
            TargetEntityId = domain.TargetEntityId,
            Status = domain.Status,
            Priority = domain.Priority,
            AttemptCount = domain.AttemptCount,
            MaxAttempts = domain.MaxAttempts,
            StartedAt = domain.StartedAt,
            NextRetryAfter = domain.NextRetryAfter,
            TimeoutSeconds = domain.TimeoutSeconds
        };
    }
}
