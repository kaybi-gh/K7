namespace K7.Shared.Dtos;

public sealed record MusicStatsDto
{
    public double TotalListeningHours { get; init; }
    public int TotalCompletedPlays { get; init; }
    public int UniqueTracksPlayed { get; init; }
    public IReadOnlyList<TopItemDto> TopArtists { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopAlbums { get; init; } = [];
    public IReadOnlyList<TopItemDto> TopTracks { get; init; } = [];
    public IReadOnlyList<GenreStatDto> TopGenres { get; init; } = [];
}

public sealed record TopItemDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? ImageUrl { get; init; }
    public int PlayCount { get; init; }
}

public sealed record GenreStatDto
{
    public required string Genre { get; init; }
    public int PlayCount { get; init; }
}
