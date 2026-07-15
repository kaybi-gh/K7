using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record PeerRejectRequest(string ProviderUrl);

public sealed record ProviderRevokeRequest(string ProviderUrl);

public sealed record FederationSessionRequest
{
    public required Guid FileId { get; init; }
    public required string UserDisplayName { get; init; }
    public required PlaybackState State { get; init; }
    public required double Position { get; init; }
}

public sealed record PeerMediaNotifyRequest
{
    public required Guid LibraryId { get; init; }
    public required Guid MediaId { get; init; }
    public required PeerMediaNotificationType Type { get; init; }
}

public sealed record ShareUpdateNotifyRequest(IReadOnlyList<Guid> SharedLibraryIds);
