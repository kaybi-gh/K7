using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Common.Interfaces;

public interface IPeerClient
{
    Task SendPeerRequestAsync(string remoteUrl, string localServerName, string localServerUrl, string token, CancellationToken cancellationToken = default);
    Task SendPeerConfirmAsync(string remoteUrl, string token, string clientId, string clientSecret, string? federationAssertionSecret = null, CancellationToken cancellationToken = default);
    Task SendPeerRejectAsync(string requesterUrl, string providerUrl, CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerLibraryDto>> GetRemoteLibrariesAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerMediaDto>> GetRemoteMediaAsync(string baseUrl, string accessToken, Guid libraryId, CancellationToken cancellationToken = default);
    Task<PeerFullMediaMetadataDto?> GetRemoteMediaMetadataAsync(string baseUrl, string accessToken, Guid mediaId, CancellationToken cancellationToken = default);
    Task<IndexedFileDto?> GetRemoteFileDetailsAsync(string baseUrl, string accessToken, Guid fileId, CancellationToken cancellationToken = default);
    Task<StreamingSessionDto?> CreateRemoteStreamSessionAsync(string baseUrl, string accessToken, CreateFederationStreamSessionRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> ProxyStreamContentAsync(string baseUrl, string accessToken, Guid sessionId, string path, CancellationToken cancellationToken = default);
    Task NotifyMediaAsync(string baseUrl, string accessToken, Guid libraryId, Guid mediaId, PeerMediaNotificationType type, CancellationToken cancellationToken = default);
    Task NotifyRevocationAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default);
    Task NotifyProviderRevocationAsync(string requesterUrl, string providerUrl, CancellationToken cancellationToken = default);
    Task NotifyShareUpdateAsync(string baseUrl, string accessToken, IReadOnlyList<Guid> sharedLibraryIds, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default);
    Task<bool> IsReachableAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedUserRef>> GetRemoteSocialUsersAsync(string baseUrl, string accessToken, string viewerAssertion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedReviewDto>> GetRemoteSocialReviewsAsync(string baseUrl, string accessToken, string viewerAssertion, Guid originUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedCollectionDto>> GetRemoteSocialCollectionsAsync(string baseUrl, string accessToken, string viewerAssertion, Guid originUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedPlaylistDto>> GetRemoteSocialPlaylistsAsync(string baseUrl, string accessToken, string viewerAssertion, Guid originUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedSmartPlaylistDto>> GetRemoteSocialSmartPlaylistsAsync(string baseUrl, string accessToken, string viewerAssertion, Guid originUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederationPlaybackEntryDto>> GetRemotePlaybackHistoryAsync(string baseUrl, string accessToken, string viewerAssertion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedUserPlaybackEntryDto>> GetRemoteSocialPlaybackHistoryAsync(string baseUrl, string accessToken, string viewerAssertion, Guid originUserId, CancellationToken cancellationToken = default);
}
