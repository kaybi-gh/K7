namespace K7.Shared.Dtos.Entities;

public sealed record ExternalIdDto
{
    public Guid Id { get; init; }
    public required string ProviderName { get; init; }
    public required string Value { get; init; }
}
