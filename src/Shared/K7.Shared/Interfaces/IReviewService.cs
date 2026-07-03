using K7.Shared.Dtos.Entities.Reviews;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IReviewService
{
    Task<IReadOnlyList<MediaReviewDto>> GetMediaReviewsAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedReviewDto>> GetFederatedMediaReviewsAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task UpsertMediaReviewAsync(Guid mediaId, UpsertMediaReviewRequest request, CancellationToken cancellationToken = default);
    Task<FederationPrivacySettingsDto> GetFederationPrivacyAsync(CancellationToken cancellationToken = default);
    Task UpdateFederationPrivacyAsync(FederationPrivacySettingsDto settings, CancellationToken cancellationToken = default);
    Task<ReviewPreferencesDto> GetReviewPreferencesAsync(CancellationToken cancellationToken = default);
    Task UpdateReviewPreferencesAsync(ReviewPreferencesDto preferences, CancellationToken cancellationToken = default);
    Task<FederationSocialPolicyDto> GetFederationSocialPolicyAsync(CancellationToken cancellationToken = default);
    Task UpdateFederationSocialPolicyAsync(FederationSocialPolicyDto policy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederationGrantTargetDto>> GetFederationGrantTargetsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedCollectionViewDto>> GetFederatedCollectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedPlaylistViewDto>> GetFederatedPlaylistsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedSmartPlaylistViewDto>> GetFederatedSmartPlaylistsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedPlaybackHistoryViewDto>> GetFederatedPlaybackHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserReviewViewDto>> GetMyMediaReviewsAsync(CancellationToken cancellationToken = default);
    Task<int> GetMyMediaReviewCountAsync(CancellationToken cancellationToken = default);
    Task<MyMediaReviewStateDto?> GetMyMediaReviewAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task DeleteMediaReviewAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
