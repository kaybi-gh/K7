namespace K7.Shared.Dtos.Requests;

public sealed record BulkUpsertMediaStatesRequest
{
    public required IReadOnlyList<MediaStateItem> Items { get; init; }
    public MergeStrategy? Strategy { get; init; }

    public sealed record MediaStateItem
    {
        public required Guid MediaId { get; init; }
        public int PlayCount { get; init; }
        public double LastPlaybackPosition { get; init; }
        public double ProgressPercentage { get; init; }
        public bool IsCompleted { get; init; }
        public DateTime? LastInteractedAt { get; init; }
    }
}
