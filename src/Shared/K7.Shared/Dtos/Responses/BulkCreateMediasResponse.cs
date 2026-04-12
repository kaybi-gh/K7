namespace K7.Shared.Dtos.Responses;

public sealed record BulkCreateMediasResponse
{
    public required IReadOnlyList<BulkCreateMediaResult> Results { get; init; }

    public sealed record BulkCreateMediaResult
    {
        public required string Key { get; init; }
        public required Guid MediaId { get; init; }
        public bool WasCreated { get; init; }
    }
}
