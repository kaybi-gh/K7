using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedMediaRef
{
    public required Guid RemoteMediaId { get; init; }
    public IReadOnlyList<PeerExternalIdDto> ExternalIds { get; init; } = [];
    public required MediaType Type { get; init; }
    public string? Title { get; init; }
}
