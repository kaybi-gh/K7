namespace K7.Clients.Shared.Models;

public sealed record MusicRadioRequest
{
    public required string RadioType { get; init; }
    public required string Title { get; init; }
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid? SeedTrackId { get; init; }
    public Guid? SeedArtistId { get; init; }
    public string? MoodPreset { get; init; }
    public int? MoodCentroidIndex { get; init; }
}
