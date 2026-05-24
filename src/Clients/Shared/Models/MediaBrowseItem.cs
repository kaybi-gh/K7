namespace K7.Clients.Shared.Models;

public sealed class MediaBrowseItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? ArtworkUrl { get; init; }
    public bool IsPlayable { get; init; }
    public bool IsBrowsable { get; init; }
}
