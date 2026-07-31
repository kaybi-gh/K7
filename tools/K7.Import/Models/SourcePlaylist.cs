namespace K7.Import.Models;

public sealed record SourcePlaylist
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? MediaType { get; init; }
    public bool IsDynamic { get; init; }
    public List<SourcePlaylistItem> Items { get; init; } = [];
}

public sealed record SourcePlaylistItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public Dictionary<string, string> ProviderIds { get; init; } = [];
    public List<string> FilePaths { get; init; } = [];
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public int? Year { get; init; }
    public string? SeriesTitle { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
}
