namespace K7.Shared.Dtos;

public sealed record MediaBrowseFacetsDto
{
    public IReadOnlyList<string> ContentRatings { get; init; } = [];
    public IReadOnlyList<string> Studios { get; init; } = [];
    public IReadOnlyList<string> Networks { get; init; } = [];
}
