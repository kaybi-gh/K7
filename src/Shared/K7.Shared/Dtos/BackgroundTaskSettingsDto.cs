using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record BackgroundTaskSettingsDto
{
    public required int WorkerCount { get; init; }

    /// <summary>
    /// One entry per lane, always the full fixed set, so the admin form is a stable grid instead of a
    /// list discovered from whatever happens to sit in the database.
    /// </summary>
    public required IReadOnlyList<LaneLimitDto> Lanes { get; init; }
}

public sealed record LaneLimitDto
{
    public required BackgroundTaskLane Lane { get; init; }

    /// <summary>Configured parallelism. Zero pauses the lane.</summary>
    public required int Limit { get; init; }

    /// <summary>Tasks currently running in this lane. Read-only.</summary>
    public required int ActiveCount { get; init; }

    /// <summary>Tasks waiting in this lane. Read-only.</summary>
    public required int PendingCount { get; init; }
}

public sealed record UpdateBackgroundTaskSettingsRequest
{
    public int? WorkerCount { get; init; }
    public Dictionary<BackgroundTaskLane, int>? LaneLimits { get; init; }
}
