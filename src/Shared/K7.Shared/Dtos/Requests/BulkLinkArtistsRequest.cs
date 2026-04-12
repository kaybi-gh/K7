namespace K7.Shared.Dtos.Requests;

public sealed record BulkLinkArtistsRequest
{
    public required IReadOnlyList<ArtistLinkItem> Items { get; init; }

    public sealed record ArtistLinkItem
    {
        public required Guid MediaId { get; init; }
        public required string ArtistName { get; init; }
    }
}
