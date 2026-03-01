using K7.Server.Domain.Entities.Users;

namespace K7.Shared.Dtos.Entities;

public sealed record UserMediaStateDto
{
    public double LastPlaybackPosition { get; init; }
    public double ProgressPercentage { get; init; }
    public bool IsCompleted { get; init; }
    public int PlayCount { get; init; }
    public DateTime? LastInteractedAt { get; init; }

    public static UserMediaStateDto FromDomain(UserMediaState domain) => new()
    {
        LastPlaybackPosition = domain.LastPlaybackPosition,
        ProgressPercentage = domain.ProgressPercentage,
        IsCompleted = domain.IsCompleted,
        PlayCount = domain.PlayCount,
        LastInteractedAt = domain.LastInteractedAt
    };
}
