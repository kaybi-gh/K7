using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public class SocialUserProfileService(
    ISocialUserDirectoryService directoryService,
    ISocialUserProfileReader profileReader)
    : ISocialUserProfileService
{
    public Task<IReadOnlyList<SocialUserDirectoryEntryDto>> GetDirectoryAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        directoryService.GetDirectoryAsync(viewerUserId, cancellationToken);

    public Task<SocialUserProfileDto?> GetLocalProfileAsync(
        Guid ownerUserId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        profileReader.GetLocalProfileAsync(ownerUserId, viewerUserId, cancellationToken);

    public Task<SocialUserProfileDto?> GetFederatedProfileAsync(
        Guid peerServerId,
        Guid originUserId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        profileReader.GetFederatedProfileAsync(peerServerId, originUserId, viewerUserId, cancellationToken);

    public Task<IReadOnlyList<SharedCollectionBrowseDto>> GetSharedCollectionsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        profileReader.GetSharedCollectionsAsync(viewerUserId, cancellationToken);

    public Task<IReadOnlyList<SharedPlaylistBrowseDto>> GetSharedPlaylistsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        profileReader.GetSharedPlaylistsAsync(viewerUserId, cancellationToken);

    public Task<bool> IsDirectoryVisibleAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        directoryService.IsDirectoryVisibleAsync(viewerUserId, cancellationToken);
}
