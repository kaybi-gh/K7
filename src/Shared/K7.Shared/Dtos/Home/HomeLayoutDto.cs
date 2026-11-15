namespace K7.Shared.Dtos.Home;

public sealed record HomeLayoutDto
{
    /// <summary>Most rows a home can hold. Each row is its own feed request.</summary>
    public const int MaxRows = 50;

    /// <summary>Bounds of <see cref="HomeRowConfigDto.AutoAdvanceSeconds"/>: long enough to read a title.</summary>
    public const int MinAutoAdvanceSeconds = 4;

    public const int MaxAutoAdvanceSeconds = 60;

    public required IReadOnlyList<HomeRowConfigDto> Rows { get; init; }
}
