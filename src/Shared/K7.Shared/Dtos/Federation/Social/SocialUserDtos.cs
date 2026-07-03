using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record SocialUserIdentityDto
{
    public required bool IsFederated { get; init; }
    public Guid? LocalUserId { get; init; }
    public Guid? PeerServerId { get; init; }
    public Guid? OriginUserId { get; init; }
    public required string DisplayName { get; init; }
    public string? PeerName { get; init; }
    public Guid? AvatarPictureId { get; init; }
}

public sealed record SocialUserDirectoryEntryDto
{
    public required SocialUserIdentityDto Identity { get; init; }
}

public sealed record SocialUserMediaCardDto
{
    public required FederatedMediaRef Media { get; init; }
    public FederatedSocialItemStatus Status { get; init; }
    public Guid? LocalMediaId { get; init; }
    public Guid? RemoteIndexedFileId { get; init; }
    public Guid? CoverPictureId { get; init; }
}

public sealed record SocialUserReviewViewDto
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public string? Emoji { get; init; }
    public int Rating { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required SocialUserMediaCardDto Media { get; init; }
}

public sealed record SocialUserPlaybackViewDto
{
    public Guid? LocalMediaId { get; init; }
    public string? MediaTitle { get; init; }
    public MediaType? MediaType { get; init; }
    public string? MediaHref { get; init; }
    public string? ImageUrl { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
}

public sealed record SocialUserCollectionCardDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public Guid? CoverPictureId { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<SocialUserMediaCardDto> PreviewItems { get; init; } = [];
}

public sealed record SocialUserPlaylistCardDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public bool IsSmart { get; init; }
    public Guid? CoverPictureId { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<SocialUserMediaCardDto> PreviewItems { get; init; } = [];
}

public sealed record SocialUserProfileVisibleSectionsDto
{
    public bool Reviews { get; init; }
    public bool PlaybackHistory { get; init; }
    public bool Collections { get; init; }
    public bool Playlists { get; init; }
    public bool SmartPlaylists { get; init; }
}

public sealed record SocialUserProfileDto
{
    public required SocialUserIdentityDto Identity { get; init; }
    public SocialUserProfileVisibleSectionsDto VisibleSections { get; init; } = new();
    public IReadOnlyList<SocialUserReviewViewDto> RecentReviews { get; init; } = [];
    public IReadOnlyList<SocialUserPlaybackViewDto> RecentPlayback { get; init; } = [];
    public IReadOnlyList<SocialUserCollectionCardDto> Collections { get; init; } = [];
    public IReadOnlyList<SocialUserPlaylistCardDto> Playlists { get; init; } = [];
    public IReadOnlyList<SocialUserPlaylistCardDto> SmartPlaylists { get; init; } = [];
}

public sealed record SharedCollectionBrowseDto
{
    public required SocialUserIdentityDto Owner { get; init; }
    public required Guid CollectionId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public int ItemCount { get; init; }
}

public sealed record SharedPlaylistBrowseDto
{
    public required SocialUserIdentityDto Owner { get; init; }
    public required Guid PlaylistId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public bool IsSmart { get; init; }
    public int ItemCount { get; init; }
}
