using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface ISocialUserProfileVisibilityService
{
    Task<bool> IsLocalUserDiscoverableAsync(Guid viewerUserId, Guid ownerUserId, CancellationToken cancellationToken = default);
    bool IsFederatedUserDiscoverableForViewer(FederationPrivacySettingsDto viewerPrivacy, FederatedUserRef remoteUser, Guid peerServerId);
    Task<bool> CanViewFederatedUserAsync(Guid viewerUserId, FederationPrivacySettingsDto viewerPrivacy, FederatedUserRef remoteUser, Guid peerServerId, CancellationToken cancellationToken = default);
    Task<SocialUserProfileVisibleSectionsDto> BuildLocalVisibleSectionsAsync(bool isSelf, Guid viewerUserId, Guid ownerUserId, FederationPrivacySettingsDto ownerPrivacy, FederationPrivacySettingsDto viewerPrivacy, CancellationToken cancellationToken = default);
    Task<SocialUserProfileVisibleSectionsDto> BuildFederatedVisibleSectionsAsync(FederationPrivacySettingsDto viewerPrivacy, FederatedUserRef remoteUser, Guid peerServerId, CancellationToken cancellationToken = default);
    Task<bool> HasAnyInboundSocialEnabledAsync(Guid peerServerId, CancellationToken cancellationToken = default);
}

public class SocialUserProfileVisibilityService(
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : ISocialUserProfileVisibilityService
{
    public async Task<bool> IsLocalUserDiscoverableAsync(
        Guid viewerUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var ownerPrivacy = await privacyService.GetPrivacyAsync(ownerUserId, cancellationToken);
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);

        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (!SocialViewVisibilityHelper.CanViewerSeeLocalContent(viewerPrivacy, contentType, ownerUserId))
                continue;

            if (await IsDiscoverableLocalContentTypeAsync(
                    viewerUserId,
                    ownerUserId,
                    contentType,
                    SocialViewVisibilityHelper.GetShareScope(ownerPrivacy, contentType),
                    cancellationToken))
                return true;
        }

        return false;
    }

    public bool IsFederatedUserDiscoverableForViewer(
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId)
    {
        var contentTypes = remoteUser.DiscoverableContentTypes.Count > 0
            ? remoteUser.DiscoverableContentTypes
            : Enum.GetValues<FederationContentType>();

        return contentTypes.Any(contentType =>
            SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId));
    }

    public async Task<bool> CanViewFederatedUserAsync(
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId,
        CancellationToken cancellationToken = default)
    {
        var contentTypes = remoteUser.DiscoverableContentTypes.Count > 0
            ? remoteUser.DiscoverableContentTypes
            : Enum.GetValues<FederationContentType>();

        foreach (var contentType in contentTypes)
        {
            if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId))
                continue;

            if (await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
                return true;
        }

        return false;
    }

    public async Task<SocialUserProfileVisibleSectionsDto> BuildLocalVisibleSectionsAsync(
        bool isSelf,
        Guid viewerUserId,
        Guid ownerUserId,
        FederationPrivacySettingsDto ownerPrivacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken = default) =>
        new()
        {
            Reviews = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Reviews, cancellationToken),
            PlaybackHistory = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.PlaybackHistory, cancellationToken),
            Collections = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Collections, cancellationToken),
            Playlists = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Playlists, cancellationToken),
            SmartPlaylists = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.SmartPlaylists, cancellationToken)
        };

    public async Task<SocialUserProfileVisibleSectionsDto> BuildFederatedVisibleSectionsAsync(
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId,
        CancellationToken cancellationToken = default) =>
        new()
        {
            Reviews = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Reviews, cancellationToken),
            PlaybackHistory = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.PlaybackHistory, cancellationToken),
            Collections = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Collections, cancellationToken),
            Playlists = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Playlists, cancellationToken),
            SmartPlaylists = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.SmartPlaylists, cancellationToken)
        };

    public async Task<bool> HasAnyInboundSocialEnabledAsync(Guid peerServerId, CancellationToken cancellationToken = default)
    {
        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
                return true;
        }

        return false;
    }

    private async Task<bool> IsDiscoverableLocalContentTypeAsync(Guid viewerUserId, Guid ownerUserId, FederationContentType contentType, VisibilityScope shareScope, CancellationToken cancellationToken)
    {
        if (shareScope == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, ownerUserId, contentType, shareScope, cancellationToken: cancellationToken))
            return false;

        return contentType == FederationContentType.PlaybackHistory
            || await HasLocalContentAsync(ownerUserId, contentType, cancellationToken);
    }

    private async Task<bool> HasLocalContentAsync(Guid ownerUserId, FederationContentType contentType, CancellationToken cancellationToken) =>
        contentType switch
        {
            FederationContentType.Reviews => await context.MediaReviews.AnyAsync(r => r.UserId == ownerUserId, cancellationToken),
            FederationContentType.Collections => await context.Collections.AnyAsync(c => c.UserId == ownerUserId && c.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.Playlists => await context.Playlists.AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.SmartPlaylists => await context.Playlists.OfType<SmartPlaylist>().AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.PlaybackHistory => await context.MediaPlaybackSessions.AnyAsync(s => s.UserId == ownerUserId, cancellationToken),
            _ => false
        };

    private async Task<bool> IsLocalSectionVisibleAsync(bool isSelf, Guid viewerUserId, Guid ownerUserId, FederationPrivacySettingsDto ownerPrivacy, FederationPrivacySettingsDto viewerPrivacy, FederationContentType contentType, CancellationToken cancellationToken)
    {
        if (isSelf)
            return true;

        if (!SocialViewVisibilityHelper.CanViewerSeeLocalContent(viewerPrivacy, contentType, ownerUserId))
            return false;

        var shareScope = SocialViewVisibilityHelper.GetShareScope(ownerPrivacy, contentType);
        return shareScope != VisibilityScope.Nobody
            && await visibilityEvaluator.CanViewAsync(viewerUserId, ownerUserId, contentType, shareScope, cancellationToken: cancellationToken);
    }

    private async Task<bool> IsFederatedSectionVisibleAsync(FederationPrivacySettingsDto viewerPrivacy, FederatedUserRef remoteUser, Guid peerServerId, FederationContentType contentType, CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId)
            || !await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
            return false;

        return remoteUser.DiscoverableContentTypes.Count == 0
            || remoteUser.DiscoverableContentTypes.Contains(contentType);
    }
}
