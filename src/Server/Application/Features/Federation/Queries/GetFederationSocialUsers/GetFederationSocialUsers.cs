using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialUsers;

public record GetFederationSocialUsersQuery(string? ClientId, string? ViewerAssertion)
    : IRequest<IReadOnlyList<FederatedUserRef>>;

public class GetFederationSocialUsersQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : IRequestHandler<GetFederationSocialUsersQuery, IReadOnlyList<FederatedUserRef>>
{
    public async Task<IReadOnlyList<FederatedUserRef>> Handle(
        GetFederationSocialUsersQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var result = new List<FederatedUserRef>();
        foreach (var user in users)
        {
            var privacy = await privacyService.GetPrivacyAsync(user.Id, cancellationToken);
            var discoverableTypes = await GetDiscoverableContentTypesAsync(
                viewer.OriginUserId,
                peer.Id,
                user.Id,
                privacy,
                cancellationToken);

            if (discoverableTypes.Count == 0)
                continue;

            result.Add(new FederatedUserRef
            {
                OriginUserId = user.Id,
                DisplayName = user.DisplayName,
                DiscoverableContentTypes = discoverableTypes
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<FederationContentType>> GetDiscoverableContentTypesAsync(
        Guid viewerOriginUserId,
        Guid peerServerId,
        Guid ownerUserId,
        FederationPrivacySettingsDto privacy,
        CancellationToken cancellationToken)
    {
        var types = new List<FederationContentType>();

        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (await IsDiscoverableContentTypeAsync(
                    viewerOriginUserId,
                    peerServerId,
                    ownerUserId,
                    contentType,
                    GetShareScope(privacy, contentType),
                    cancellationToken))
                types.Add(contentType);
        }

        return types;
    }

    private async Task<bool> IsDiscoverableContentTypeAsync(
        Guid viewerOriginUserId,
        Guid peerServerId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope shareScope,
        CancellationToken cancellationToken)
    {
        if (shareScope == VisibilityScope.Nobody)
            return false;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
            return false;

        if (!await visibilityEvaluator.CanViewFederatedAsync(
                viewerOriginUserId, peerServerId, ownerUserId, contentType, shareScope, cancellationToken: cancellationToken))
            return false;

        if (contentType == FederationContentType.PlaybackHistory)
            return true;

        return contentType switch
        {
            FederationContentType.Reviews => await context.MediaReviews.AnyAsync(r => r.UserId == ownerUserId, cancellationToken),
            FederationContentType.Collections => await context.Collections.AnyAsync(c => c.UserId == ownerUserId && c.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.Playlists => await context.Playlists.AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.SmartPlaylists => await context.Playlists.OfType<SmartPlaylist>().AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            _ => false
        };
    }

    private static VisibilityScope GetShareScope(FederationPrivacySettingsDto privacy, FederationContentType contentType) =>
        contentType switch
        {
            FederationContentType.Reviews => privacy.Share.Reviews,
            FederationContentType.Collections => privacy.Share.Collections,
            FederationContentType.Playlists => privacy.Share.Playlists,
            FederationContentType.SmartPlaylists => privacy.Share.SmartPlaylists,
            FederationContentType.PlaybackHistory => privacy.Share.PlaybackHistory,
            _ => VisibilityScope.Nobody
        };
}
