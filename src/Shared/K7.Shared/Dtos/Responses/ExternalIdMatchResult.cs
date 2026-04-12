namespace K7.Shared.Dtos.Responses;

public sealed record ExternalIdMatchResult
{
    public required string Provider { get; init; }
    public required string Value { get; init; }
    public Guid? MediaId { get; init; }
}
