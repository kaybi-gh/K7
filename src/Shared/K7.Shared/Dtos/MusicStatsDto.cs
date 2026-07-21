namespace K7.Shared.Dtos;

public sealed record TopItemDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? ImageUrl { get; init; }
    public string? MediaType { get; init; }
    public int PlayCount { get; init; }
    /// <summary>Album id for tracks, or serie id for episodes/seasons.</summary>
    public Guid? ParentId { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
}

public sealed record GenreStatDto
{
    public required string Genre { get; init; }
    public int PlayCount { get; init; }
}
