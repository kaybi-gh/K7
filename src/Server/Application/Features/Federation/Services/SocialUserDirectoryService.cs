using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface ISocialUserDirectoryService
{
    Task<IReadOnlyList<SocialUserDirectoryEntryDto>> GetDirectoryAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> IsDirectoryVisibleAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
}

public class SocialUserDirectoryService(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IFederationViewerAssertionService assertionService,
    IUserFederationPrivacyService privacyService,
    ISocialUserProfileVisibilityService profileVisibilityService,
    IIdentityService identityService)
    : ISocialUserDirectoryService
{
    public async Task<IReadOnlyList<SocialUserDirectoryEntryDto>> GetDirectoryAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SocialUserDirectoryEntryDto>();
        var localUsers = await context.Users
            .AsNoTracking()
            .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null && u.Id != viewerUserId)
            .ToListAsync(cancellationToken);

        foreach (var owner in localUsers)
        {
            if (!await profileVisibilityService.IsLocalUserDiscoverableAsync(viewerUserId, owner.Id, cancellationToken))
                continue;

            results.Add(new SocialUserDirectoryEntryDto
            {
                Identity = await ToLocalIdentityAsync(owner, cancellationToken)
            });
        }

        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        var peers = await FederationSocialConsumerHelper.GetActiveOutboundPeersAsync(context, cancellationToken);
        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);

        foreach (var peer in peers)
        {
            if (!await profileVisibilityService.HasAnyInboundSocialEnabledAsync(peer.Id, cancellationToken))
                continue;

            var token = await peerClient.GetAccessTokenAsync(
                peer.BaseUrl,
                peer.OutboundClientId!,
                peer.OutboundClientSecret!,
                cancellationToken);
            if (token is null)
                continue;

            var assertionSecret = peer.FederationAssertionSecret ?? peer.OutboundClientSecret!;
            var assertion = assertionService.CreateAssertion(new FederatedUserRef
            {
                OriginUserId = viewerUserId,
                DisplayName = viewer?.DisplayName
            }, assertionSecret);

            var remoteUsers = await peerClient.GetRemoteSocialUsersAsync(peer.BaseUrl, token, assertion, cancellationToken);
            foreach (var remoteUser in remoteUsers)
            {
                if (!profileVisibilityService.IsFederatedUserDiscoverableForViewer(viewerPrivacy, remoteUser, peer.Id))
                    continue;

                results.Add(new SocialUserDirectoryEntryDto
                {
                    Identity = new SocialUserIdentityDto
                    {
                        IsFederated = true,
                        PeerServerId = peer.Id,
                        OriginUserId = remoteUser.OriginUserId,
                        DisplayName = remoteUser.DisplayName ?? "?",
                        PeerName = peer.Name
                    }
                });
            }
        }

        return results.OrderBy(entry => entry.Identity.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<bool> IsDirectoryVisibleAsync(Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        var privacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (!HasSocialViewEnabled(privacy.View))
            return false;

        return (await GetDirectoryAsync(viewerUserId, cancellationToken)).Count > 0;
    }

    private async Task<SocialUserIdentityDto> ToLocalIdentityAsync(User owner, CancellationToken cancellationToken)
    {
        var avatarId = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId == owner.Id && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new SocialUserIdentityDto
        {
            IsFederated = false,
            LocalUserId = owner.Id,
            DisplayName = await LocalUserDisplayNameHelper.ResolveAsync(identityService, owner, cancellationToken),
            AvatarPictureId = avatarId
        };
    }

    private static bool HasSocialViewEnabled(FederationContentVisibilityDto view) =>
        view.Reviews != VisibilityScope.Nobody
        || view.Collections != VisibilityScope.Nobody
        || view.Playlists != VisibilityScope.Nobody
        || view.SmartPlaylists != VisibilityScope.Nobody
        || view.PlaybackHistory != VisibilityScope.Nobody;
}
