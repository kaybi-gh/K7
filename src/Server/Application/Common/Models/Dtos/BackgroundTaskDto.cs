using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Models.Dtos;

public record BackgroundTaskDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? TargetEntityType { get; init; }
    public Guid? TargetEntityId { get; init; }
    public BackgroundTaskStatus Status { get; init; }
    public int Priority { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetryCount { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BackgroundTask, BackgroundTaskDto>();
        }
    }
}
