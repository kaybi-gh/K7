namespace K7.Shared.Dtos;

public sealed record MediaGenreDto
{
    public required string Name { get; init; }
    public int MediaCount { get; init; }
}
