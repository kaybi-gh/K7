namespace K7.Shared.Dtos;

public sealed record MusicIntelligenceTrackMatchDto
{
    public required Guid ItemId { get; init; }

    /// <summary>Raw AudioMuse score (distance, similarity, …).</summary>
    public double? Score { get; init; }

    /// <summary>AudioMuse metric name: distance, similarity, etc.</summary>
    public string? ScoreMetric { get; init; }
}
