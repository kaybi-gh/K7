namespace K7.Shared.Dtos.Entities;

public sealed record ExternalIdEditDto
{
    public required string ProviderName { get; init; }
    public required string Value { get; init; }
}
