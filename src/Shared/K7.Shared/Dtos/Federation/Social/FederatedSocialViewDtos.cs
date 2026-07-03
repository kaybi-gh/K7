using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedCollectionViewDto
{
    public required Guid PeerServerId { get; init; }
    public required string PeerName { get; init; }
    public required Guid OriginUserId { get; init; }
    public required FederatedCollectionDto Collection { get; init; }
    public required string AuthorName { get; init; }
    public IReadOnlyList<FederatedSocialItemViewDto> Items { get; init; } = [];
}

public sealed record FederatedPlaylistViewDto
{
    public required Guid PeerServerId { get; init; }
    public required string PeerName { get; init; }
    public required Guid OriginUserId { get; init; }
    public required FederatedPlaylistDto Playlist { get; init; }
    public required string AuthorName { get; init; }
    public IReadOnlyList<FederatedSocialItemViewDto> Items { get; init; } = [];
}

public sealed record FederatedSmartPlaylistViewDto
{
    public required Guid PeerServerId { get; init; }
    public required string PeerName { get; init; }
    public required Guid OriginUserId { get; init; }
    public required FederatedSmartPlaylistDto Playlist { get; init; }
    public required string AuthorName { get; init; }
    public IReadOnlyList<FederatedSocialItemViewDto> Items { get; init; } = [];
}

public sealed record FederatedSocialItemViewDto
{
    public required FederatedMediaRef Media { get; init; }
    public FederatedSocialItemStatus Status { get; init; }
    public Guid? LocalMediaId { get; init; }
    public Guid? RemoteIndexedFileId { get; init; }
}

public enum FederatedSocialItemStatus
{
    ResolvedLocal = 0,
    ResolvedRemote = 1,
    Unavailable = 2
}

public sealed record FederatedPlaybackHistoryViewDto
{
    public required Guid PeerServerId { get; init; }
    public required string PeerName { get; init; }
    public required string UserDisplayName { get; init; }
    public Guid? LocalMediaId { get; init; }
    public string? MediaTitle { get; init; }
    public TimeSpan Position { get; init; }
    public DateTimeOffset EndedAt { get; init; }
}
