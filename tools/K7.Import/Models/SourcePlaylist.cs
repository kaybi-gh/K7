namespace K7.Import.Models;

public sealed record SourcePlaylist
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public List<SourcePlaylistItem> Items { get; init; } = [];
}

public sealed record SourcePlaylistItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public Dictionary<string, string> ProviderIds { get; init; } = [];
}
