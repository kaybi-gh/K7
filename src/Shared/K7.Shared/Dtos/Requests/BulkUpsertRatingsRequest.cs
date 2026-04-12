namespace K7.Shared.Dtos.Requests;

public sealed record BulkUpsertRatingsRequest
{
    public required IReadOnlyList<RatingItem> Items { get; init; }
    public MergeStrategy? Strategy { get; init; }

    public sealed record RatingItem
    {
        public required Guid MediaId { get; init; }
        public required double Value { get; init; }
    }
}
