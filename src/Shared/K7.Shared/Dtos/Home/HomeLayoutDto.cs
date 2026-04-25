namespace K7.Shared.Dtos.Home;

public sealed record HomeLayoutDto
{
    public required IReadOnlyList<HomeRowConfigDto> Rows { get; init; }
}
