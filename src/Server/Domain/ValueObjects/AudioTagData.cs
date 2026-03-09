namespace K7.Server.Domain.ValueObjects;

public sealed record AudioTagData
{
    public string? Title { get; init; }
    public string? Album { get; init; }
    public IReadOnlyList<string> Artists { get; init; } = [];
    public IReadOnlyList<string> AlbumArtists { get; init; } = [];
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public int? Year { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string? Lyrics { get; init; }
    public double? Bpm { get; init; }
    public byte[]? CoverArtData { get; init; }
    public string? CoverArtMimeType { get; init; }
}
