using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public class ContentVisibilityEvaluator(
    IApplicationDbContext context,
    IFederationSocialPolicyService policyService) : IContentVisibilityEvaluator
{
    public async Task<bool> IsFederationSocialEnabledAsync(
        FederationContentType contentType,
        bool outbound,
        Guid? peerServerId = null,
        CancellationToken cancellationToken = default)
    {
        var policy = await policyService.GetAsync(cancellationToken);
        if (!policy.Enabled)
            return false;

        if (!policy.Policies.TryGetValue(contentType, out var typePolicy))
            return false;

        if (outbound ? !typePolicy.Outbound : !typePolicy.Inbound)
            return false;

        if (peerServerId is null)
            return true;

        var peerAgreement = await context.PeerSocialAgreements
            .FirstOrDefaultAsync(a => a.PeerServerId == peerServerId && a.ContentType == contentType, cancellationToken);

        if (peerAgreement is null)
            return true;

        return outbound ? peerAgreement.AllowOutbound : peerAgreement.AllowInbound;
    }

    public async Task<bool> CanShareAsync(
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope scope,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (scope == VisibilityScope.Nobody)
            return false;

        if (scope == VisibilityScope.LocalServer)
            return true;

        if (!await IsFederationSocialEnabledAsync(contentType, outbound: true, cancellationToken: cancellationToken))
            return false;

        return scope switch
        {
            VisibilityScope.Federation => true,
            VisibilityScope.SpecificPeople => true,
            _ => false
        };
    }

    public async Task<bool> CanViewAsync(
        Guid viewerUserId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope ownerScope,
        Guid? ownerPeerServerId = null,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId == ownerUserId)
            return true;

        if (ownerScope == VisibilityScope.Nobody)
            return false;

        if (ownerScope == VisibilityScope.LocalServer)
        {
            if (ownerPeerServerId is not null)
                return false;

            return true;
        }

        if (ownerPeerServerId is not null)
        {
            if (!await IsFederationSocialEnabledAsync(contentType, outbound: false, ownerPeerServerId, cancellationToken))
                return false;
        }
        else if (!await IsFederationSocialEnabledAsync(contentType, outbound: false, cancellationToken: cancellationToken))
        {
            return false;
        }

        return ownerScope switch
        {
            VisibilityScope.Federation => true,
            VisibilityScope.SpecificPeople => await HasMatchingGrantAsync(
                ownerUserId,
                contentType,
                playlistId,
                collectionId,
                targetUserId: viewerUserId,
                targetPeerServerId: null,
                targetOriginUserId: null,
                cancellationToken)
                || await HasMatchingGrantForFederatedViewerAsync(
                    ownerUserId,
                    contentType,
                    playlistId,
                    collectionId,
                    viewerUserId,
                    cancellationToken),
            _ => false
        };
    }

    public async Task<bool> CanViewFederatedAsync(
        Guid viewerOriginUserId,
        Guid viewerPeerServerId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope ownerScope,
        Guid? playlistId = null,
        Guid? collectionId = null,
        CancellationToken cancellationToken = default)
    {
        if (ownerScope == VisibilityScope.Nobody)
            return false;

        if (ownerScope == VisibilityScope.LocalServer)
            return false;

        if (!await IsFederationSocialEnabledAsync(contentType, outbound: false, viewerPeerServerId, cancellationToken))
            return false;

        return ownerScope switch
        {
            VisibilityScope.Federation => true,
            VisibilityScope.SpecificPeople => await HasMatchingGrantAsync(
                ownerUserId,
                contentType,
                playlistId,
                collectionId,
                targetUserId: null,
                targetPeerServerId: viewerPeerServerId,
                targetOriginUserId: viewerOriginUserId,
                cancellationToken),
            _ => false
        };
    }

    private async Task<bool> HasMatchingGrantAsync(
        Guid ownerUserId,
        FederationContentType contentType,
        Guid? playlistId,
        Guid? collectionId,
        Guid? targetUserId,
        Guid? targetPeerServerId,
        Guid? targetOriginUserId,
        CancellationToken cancellationToken)
    {
        var grants = await context.VisibilityGrants
            .Where(g => g.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

        return grants.Any(g => MatchesGrant(g, contentType, playlistId, collectionId, targetUserId, targetPeerServerId, targetOriginUserId));
    }

    private async Task<bool> HasMatchingGrantForFederatedViewerAsync(
        Guid ownerUserId,
        FederationContentType contentType,
        Guid? playlistId,
        Guid? collectionId,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        var viewer = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);

        if (viewer?.PeerServerId is null)
            return false;

        var grants = await context.VisibilityGrants
            .Where(g => g.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);

        return grants.Any(g => MatchesGrant(
            g,
            contentType,
            playlistId,
            collectionId,
            targetUserId: null,
            targetPeerServerId: viewer.PeerServerId,
            targetOriginUserId: viewer.OriginUserId));
    }

    private static bool MatchesGrant(
        Domain.Entities.Federation.VisibilityGrant grant,
        FederationContentType contentType,
        Guid? playlistId,
        Guid? collectionId,
        Guid? targetUserId,
        Guid? targetPeerServerId,
        Guid? targetOriginUserId)
    {
        if (grant.ContentType is not null && grant.ContentType != contentType)
            return false;

        if (grant.PlaylistId is not null && grant.PlaylistId != playlistId)
            return false;

        if (grant.CollectionId is not null && grant.CollectionId != collectionId)
            return false;

        if (grant.TargetUserId is not null && grant.TargetUserId != targetUserId)
            return false;

        if (grant.TargetPeerServerId is not null && grant.TargetPeerServerId != targetPeerServerId)
            return false;

        if (grant.TargetOriginUserId is not null && grant.TargetOriginUserId != targetOriginUserId)
            return false;

        return grant.TargetUserId is not null
            || grant.TargetPeerServerId is not null
            || grant.TargetOriginUserId is not null;
    }
}
