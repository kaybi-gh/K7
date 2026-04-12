namespace K7.Shared.Dtos.Requests;

public sealed record LookupMediasByExternalIdsRequest
{
    public required IReadOnlyList<ExternalIdItem> Items { get; init; }

    public sealed record ExternalIdItem
    {
        public required string Provider { get; init; }
        public required string Value { get; init; }
    }
}
