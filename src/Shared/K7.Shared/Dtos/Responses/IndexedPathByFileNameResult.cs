namespace K7.Shared.Dtos.Responses;

public sealed record IndexedPathByFileNameResult
{
    public required string FileName { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
}
