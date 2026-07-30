namespace K7.Shared.Dtos.Responses;

public sealed record PathMatchResult
{
    public required string Path { get; init; }
    public Guid? MediaId { get; init; }
}
