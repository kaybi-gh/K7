namespace K7.Shared.Dtos.Requests;

public sealed record BulkCreateMediasRequest
{
    public required IReadOnlyList<BulkCreateMediaItem> Items { get; init; }
    public bool FetchMetadata { get; init; }
    public bool CreateMissing { get; init; } = true;

    public sealed record BulkCreateMediaItem
    {
        public required string Key { get; init; }
        public required string MediaType { get; init; }
        public required string Title { get; init; }
        public string? SortTitle { get; init; }
        public int? Year { get; init; }
        public Dictionary<string, string> ExternalIds { get; init; } = [];
        public string? ArtistName { get; init; }
        public string? AlbumName { get; init; }
        public string? SeriesTitle { get; init; }
        public int? SeasonNumber { get; init; }
        public int? EpisodeNumber { get; init; }
    }
}
