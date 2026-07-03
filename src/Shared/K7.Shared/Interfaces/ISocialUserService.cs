using K7.Shared.Dtos.Federation.Social;

namespace K7.Shared.Interfaces;

public interface ISocialUserService
{
    Task<IReadOnlyList<SocialUserDirectoryEntryDto>> GetSocialUserDirectoryAsync(CancellationToken cancellationToken = default);
    Task<SocialUserProfileDto?> GetLocalUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SocialUserProfileDto?> GetFederatedUserProfileAsync(Guid peerServerId, Guid originUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedCollectionBrowseDto>> GetSharedCollectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedPlaylistBrowseDto>> GetSharedPlaylistsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CopyFederatedPlaylistAsync(Guid peerServerId, Guid originUserId, Guid playlistId, CancellationToken cancellationToken = default);
    Task<SocialDiscoveryStateDto> GetSocialDiscoveryStateAsync(CancellationToken cancellationToken = default);
}
