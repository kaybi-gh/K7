using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

internal static class FederationSocialConsumerHelper
{
    public static async Task<IReadOnlyList<PeerServer>> GetActiveOutboundPeersAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.PeerServers
            .Where(p => p.Status == PeerStatus.Active && p.OutboundClientId != null && p.OutboundClientSecret != null)
            .ToListAsync(cancellationToken);
    }

    public static bool MatchesViewGrants(
        FederationContentType contentType,
        VisibilityScope viewScope,
        IReadOnlyList<FederationVisibilityGrantDto> viewGrants,
        FederatedUserRef remoteUser,
        Guid peerServerId)
    {
        if (viewScope == VisibilityScope.Nobody)
            return false;

        if (viewScope == VisibilityScope.Federation || viewScope == VisibilityScope.LocalServer)
            return viewScope != VisibilityScope.LocalServer;

        if (viewScope != VisibilityScope.SpecificPeople)
            return false;

        return viewGrants.Any(g =>
            (g.ContentType is null || g.ContentType == contentType)
            && (
                (g.TargetPeerServerId == peerServerId && g.TargetOriginUserId is null)
                || (g.TargetPeerServerId == peerServerId && g.TargetOriginUserId == remoteUser.OriginUserId)
            ));
    }

    public static FederatedSocialItemViewDto ToItemView(FederatedMediaRef media, FederatedMediaResolutionResult resolution) =>
        new()
        {
            Media = media,
            Status = resolution.Status switch
            {
                FederatedMediaResolutionStatus.ResolvedLocal => FederatedSocialItemStatus.ResolvedLocal,
                FederatedMediaResolutionStatus.ResolvedRemote => FederatedSocialItemStatus.ResolvedRemote,
                _ => FederatedSocialItemStatus.Unavailable
            },
            LocalMediaId = resolution.LocalMediaId,
            RemoteIndexedFileId = resolution.RemoteIndexedFileId
        };
}
