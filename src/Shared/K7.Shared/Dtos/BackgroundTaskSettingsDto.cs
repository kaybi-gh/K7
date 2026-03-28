namespace K7.Shared.Dtos;

public sealed record BackgroundTaskSettingsDto
{
    public required int WorkerCount { get; init; }
    public required IReadOnlyList<ConcurrencyGroupDto> ConcurrencyGroups { get; init; }
}

public sealed record ConcurrencyGroupDto
{
    public required string Name { get; init; }
    public required int Limit { get; init; }
    public required int ActiveCount { get; init; }
}

public sealed record UpdateBackgroundTaskSettingsRequest
{
    public int? WorkerCount { get; init; }
    public Dictionary<string, int>? ConcurrencyLimits { get; init; }
}
