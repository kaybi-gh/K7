using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

internal static class SocialViewVisibilityHelper
{
    public static VisibilityScope GetViewScope(FederationPrivacySettingsDto privacy, FederationContentType contentType) =>
        contentType switch
        {
            FederationContentType.Reviews => privacy.View.Reviews,
            FederationContentType.Collections => privacy.View.Collections,
            FederationContentType.Playlists => privacy.View.Playlists,
            FederationContentType.SmartPlaylists => privacy.View.SmartPlaylists,
            FederationContentType.PlaybackHistory => privacy.View.PlaybackHistory,
            _ => VisibilityScope.Nobody
        };

    public static VisibilityScope GetShareScope(FederationPrivacySettingsDto privacy, FederationContentType contentType) =>
        contentType switch
        {
            FederationContentType.Reviews => privacy.Share.Reviews,
            FederationContentType.Collections => privacy.Share.Collections,
            FederationContentType.Playlists => privacy.Share.Playlists,
            FederationContentType.SmartPlaylists => privacy.Share.SmartPlaylists,
            FederationContentType.PlaybackHistory => privacy.Share.PlaybackHistory,
            _ => VisibilityScope.Nobody
        };

    public static bool CanViewerSeeLocalContent(
        FederationPrivacySettingsDto viewerPrivacy,
        FederationContentType contentType,
        Guid ownerUserId) =>
        GetViewScope(viewerPrivacy, contentType) switch
        {
            VisibilityScope.Nobody => false,
            VisibilityScope.LocalServer or VisibilityScope.Federation => true,
            VisibilityScope.SpecificPeople => viewerPrivacy.View.Grants.Any(g =>
                (g.ContentType is null || g.ContentType == contentType)
                && g.TargetUserId == ownerUserId
                && g.TargetPeerServerId is null),
            _ => false
        };

    public static bool CanViewerSeeFederatedContent(
        FederationPrivacySettingsDto viewerPrivacy,
        FederationContentType contentType,
        FederatedUserRef remoteUser,
        Guid peerServerId)
    {
        var viewScope = GetViewScope(viewerPrivacy, contentType);
        if (viewScope is VisibilityScope.Nobody or VisibilityScope.LocalServer)
            return false;

        if (viewScope == VisibilityScope.SpecificPeople)
            return FederationSocialConsumerHelper.MatchesViewGrants(
                contentType,
                viewScope,
                viewerPrivacy.View.Grants,
                remoteUser,
                peerServerId);

        return true;
    }
}
