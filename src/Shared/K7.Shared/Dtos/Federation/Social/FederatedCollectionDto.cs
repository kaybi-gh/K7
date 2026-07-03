using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedCollectionDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public IReadOnlyList<FederatedCollectionItemDto> Items { get; init; } = [];
}

public sealed record FederatedCollectionItemDto
{
    public required FederatedMediaRef Media { get; init; }
    public int Order { get; init; }
}
