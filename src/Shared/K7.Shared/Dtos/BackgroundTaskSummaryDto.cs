using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record BackgroundTaskSummaryDto
{
    public required int TotalCount { get; init; }
    public required IReadOnlyList<StatusCountDto> StatusCounts { get; init; }
    public required IReadOnlyList<TaskTypeCountDto> TaskTypeCounts { get; init; }
}

public sealed record StatusCountDto
{
    public required BackgroundTaskStatus Status { get; init; }
    public required int Count { get; init; }
}

public sealed record TaskTypeCountDto
{
    public required string TaskName { get; init; }
    public required int Count { get; init; }
}
