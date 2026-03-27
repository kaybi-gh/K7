namespace K7.Shared.Dtos.Entities;

public sealed record UserMediaStateDto
{
    public double LastPlaybackPosition { get; init; }
    public double ProgressPercentage { get; init; }
    public bool IsCompleted { get; init; }
    public int PlayCount { get; init; }
    public DateTime? LastInteractedAt { get; init; }
}
