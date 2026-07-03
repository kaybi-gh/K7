using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedUserRef
{
    public Guid? OriginPeerServerId { get; init; }
    public required Guid OriginUserId { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<FederationContentType> DiscoverableContentTypes { get; init; } = [];
}
