using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public interface IContentVisibilityEvaluator
{
    Task<bool> CanShareAsync(
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope scope,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanViewAsync(
        Guid viewerUserId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope ownerScope,
        Guid? ownerPeerServerId = null,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanViewFederatedAsync(
        Guid viewerOriginUserId,
        Guid viewerPeerServerId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope ownerScope,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsFederationSocialEnabledAsync(
        FederationContentType contentType,
        bool outbound,
        Guid? peerServerId = null,
        CancellationToken cancellationToken = default);
}
