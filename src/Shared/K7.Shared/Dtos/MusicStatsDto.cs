namespace K7.Shared.Dtos;

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
