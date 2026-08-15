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
        public string? OriginalTitle { get; init; }
        public int? Year { get; init; }
        public Dictionary<string, string> ExternalIds { get; init; } = [];
        /// <summary>
        /// Extra Spotify track ids for the same recording (same title, different editions).
        /// Stored alongside <see cref="ExternalIds"/> so later imports can hit this media.
        /// </summary>
        public IReadOnlyList<string> AdditionalSpotifyIds { get; init; } = [];
        /// <summary>Spotify popularity 0-100. Used to pick the canonical ISRC in a title group.</summary>
        public int? Popularity { get; init; }
        /// <summary>
        /// Parent-series provider ids (Tautulli/Plex series guids). Used to resolve the
        /// show, then match SxxExx. Must not be mixed into <see cref="ExternalIds"/>.
        /// </summary>
        public Dictionary<string, string> SeriesExternalIds { get; init; } = [];
        public string? ArtistName { get; init; }
        public string? AlbumName { get; init; }
        public string? SeriesTitle { get; init; }
        public int? SeasonNumber { get; init; }
        public int? EpisodeNumber { get; init; }
        public int? EpisodeNumberEnd { get; init; }
    }
}
