using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Reviews;
using K7.Server.Application.Features.SmartPlaylists.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Navigation;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface ISocialUserProfileService
{
    Task<IReadOnlyList<SocialUserDirectoryEntryDto>> GetDirectoryAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<SocialUserProfileDto?> GetLocalProfileAsync(Guid ownerUserId, Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<SocialUserProfileDto?> GetFederatedProfileAsync(Guid peerServerId, Guid originUserId, Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedCollectionBrowseDto>> GetSharedCollectionsAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedPlaylistBrowseDto>> GetSharedPlaylistsAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<bool> IsDirectoryVisibleAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
}

public class SocialUserProfileService(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IFederationViewerAssertionService assertionService,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator,
    IFederatedMediaResolver mediaResolver,
    IFederationSocialConsumerService consumerService,
    IIdentityService identityService)
    : ISocialUserProfileService
{
    private const int RecentLimit = 20;
    private const int PreviewItemLimit = 8;

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
            if (!await IsLocalUserDiscoverableAsync(viewerUserId, owner.Id, cancellationToken))
                continue;

            var avatarId = await GetAvatarPictureIdAsync(owner.Id, cancellationToken);
            results.Add(new SocialUserDirectoryEntryDto
            {
                Identity = await ToLocalIdentityAsync(owner, avatarId, cancellationToken)
            });
        }

        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        var peers = await FederationSocialConsumerHelper.GetActiveOutboundPeersAsync(context, cancellationToken);
        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);

        foreach (var peer in peers)
        {
            if (!await HasAnyInboundSocialEnabledAsync(peer.Id, cancellationToken))
                continue;

            var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);
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
                if (!IsFederatedUserDiscoverableForViewer(viewerPrivacy, remoteUser, peer.Id))
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

        return results
            .OrderBy(e => e.Identity.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> IsDirectoryVisibleAsync(Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (!HasSocialViewEnabled(viewerPrivacy.View))
            return false;

        var directory = await GetDirectoryAsync(viewerUserId, cancellationToken);
        return directory.Count > 0;
    }

    public async Task<SocialUserProfileDto?> GetLocalProfileAsync(
        Guid ownerUserId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var owner = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId && u.PeerServerId == null && u.IsActive && u.DeletedAt == null, cancellationToken);

        if (owner is null)
            return null;

        var isSelf = ownerUserId == viewerUserId;
        var privacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        var avatarId = await GetAvatarPictureIdAsync(owner.Id, cancellationToken);
        var identity = await ToLocalIdentityAsync(owner, avatarId, cancellationToken);

        if (!isSelf && !await IsLocalUserDiscoverableAsync(viewerUserId, owner.Id, cancellationToken))
            return null;

        var recentReviews = await LoadLocalReviewsAsync(owner.Id, viewerUserId, isSelf, privacy, viewerPrivacy, cancellationToken);
        var recentPlayback = await LoadLocalPlaybackAsync(owner.Id, viewerUserId, isSelf, privacy, viewerPrivacy, cancellationToken);
        var collections = await LoadLocalCollectionsAsync(owner, viewerUserId, isSelf, privacy, viewerPrivacy, cancellationToken);
        var playlists = await LoadLocalPlaylistsAsync(owner, viewerUserId, isSelf, privacy, viewerPrivacy, cancellationToken);
        var smartPlaylists = await LoadLocalSmartPlaylistsAsync(owner, viewerUserId, isSelf, privacy, viewerPrivacy, cancellationToken);
        var visibleSections = await BuildLocalVisibleSectionsAsync(
            isSelf,
            viewerUserId,
            owner.Id,
            privacy,
            viewerPrivacy,
            cancellationToken);

        return new SocialUserProfileDto
        {
            Identity = identity,
            VisibleSections = visibleSections,
            RecentReviews = recentReviews,
            RecentPlayback = recentPlayback,
            Collections = collections,
            Playlists = playlists,
            SmartPlaylists = smartPlaylists
        };
    }

    public async Task<SocialUserProfileDto?> GetFederatedProfileAsync(
        Guid peerServerId,
        Guid originUserId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var peer = await context.PeerServers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == peerServerId && p.Status == PeerStatus.Active, cancellationToken);

        if (peer is null || string.IsNullOrWhiteSpace(peer.OutboundClientId) || string.IsNullOrWhiteSpace(peer.OutboundClientSecret))
            return null;

        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);

        var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
        if (token is null)
            return null;

        var assertionSecret = peer.FederationAssertionSecret ?? peer.OutboundClientSecret;
        var assertion = assertionService.CreateAssertion(new FederatedUserRef
        {
            OriginUserId = viewerUserId,
            DisplayName = viewer?.DisplayName
        }, assertionSecret);

        var remoteUsers = await peerClient.GetRemoteSocialUsersAsync(peer.BaseUrl, token, assertion, cancellationToken);
        var remoteUser = remoteUsers.FirstOrDefault(u => u.OriginUserId == originUserId);
        if (remoteUser is null)
            return null;

        if (!await CanViewFederatedUserAsync(viewerUserId, viewerPrivacy, remoteUser, peer.Id, cancellationToken))
            return null;

        var identity = new SocialUserIdentityDto
        {
            IsFederated = true,
            PeerServerId = peer.Id,
            OriginUserId = originUserId,
            DisplayName = remoteUser.DisplayName ?? "?",
            PeerName = peer.Name
        };

        var recentReviews = await LoadFederatedReviewsAsync(peer, viewerUserId, viewerPrivacy, remoteUser, token, assertion, originUserId, cancellationToken);
        var recentPlayback = await LoadFederatedPlaybackAsync(peer, viewerUserId, viewerPrivacy, remoteUser, token, assertion, originUserId, cancellationToken);
        var collections = await LoadFederatedCollectionsAsync(peer, viewerUserId, viewerPrivacy, remoteUser, token, assertion, originUserId, cancellationToken);
        var playlists = await LoadFederatedPlaylistsAsync(peer, viewerUserId, viewerPrivacy, remoteUser, token, assertion, originUserId, cancellationToken);
        var smartPlaylists = await LoadFederatedSmartPlaylistsAsync(peer, viewerUserId, viewerPrivacy, remoteUser, token, assertion, originUserId, cancellationToken);
        var visibleSections = await BuildFederatedVisibleSectionsAsync(viewerPrivacy, remoteUser, peer.Id, cancellationToken);

        return new SocialUserProfileDto
        {
            Identity = identity,
            VisibleSections = visibleSections,
            RecentReviews = recentReviews,
            RecentPlayback = recentPlayback,
            Collections = collections,
            Playlists = playlists,
            SmartPlaylists = smartPlaylists
        };
    }

    public async Task<IReadOnlyList<SharedCollectionBrowseDto>> GetSharedCollectionsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.Collections == VisibilityScope.Nobody)
            return [];

        var results = new List<SharedCollectionBrowseDto>();

        var localOwners = await context.Users
            .AsNoTracking()
            .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null && u.Id != viewerUserId)
            .ToListAsync(cancellationToken);

        foreach (var owner in localOwners)
        {
            var ownerPrivacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
            if (ownerPrivacy.Share.Collections == VisibilityScope.Nobody)
                continue;

            if (!await visibilityEvaluator.CanViewAsync(
                viewerUserId, owner.Id, FederationContentType.Collections, ownerPrivacy.Share.Collections, cancellationToken: cancellationToken))
                continue;

            var collections = await context.Collections
                .AsNoTracking()
                .Include(c => c.Items)
                .Where(c => c.UserId == owner.Id && c.VisibilityScope != VisibilityScope.Nobody)
                .ToListAsync(cancellationToken);

            var avatarId = await GetAvatarPictureIdAsync(owner.Id, cancellationToken);
            var ownerIdentity = await ToLocalIdentityAsync(owner, avatarId, cancellationToken);

            foreach (var collection in collections)
            {
                if (!await visibilityEvaluator.CanViewAsync(
                    viewerUserId,
                    owner.Id,
                    FederationContentType.Collections,
                    collection.VisibilityScope,
                    collectionId: collection.Id,
                    cancellationToken: cancellationToken))
                    continue;

                results.Add(new SharedCollectionBrowseDto
                {
                    Owner = ownerIdentity,
                    CollectionId = collection.Id,
                    Title = collection.Title,
                    Description = collection.Description,
                    MediaType = collection.MediaType,
                    ItemCount = collection.Items.Count
                });
            }
        }

        var federated = await consumerService.GetCollectionsAsync(viewerUserId, cancellationToken);
        foreach (var entry in federated)
        {
            results.Add(new SharedCollectionBrowseDto
            {
                Owner = new SocialUserIdentityDto
                {
                    IsFederated = true,
                    PeerServerId = entry.PeerServerId,
                    OriginUserId = entry.OriginUserId,
                    DisplayName = entry.AuthorName.Split(" @ ").FirstOrDefault() ?? entry.AuthorName,
                    PeerName = entry.PeerName
                },
                CollectionId = entry.Collection.Id,
                Title = entry.Collection.Title,
                Description = entry.Collection.Description,
                MediaType = entry.Collection.MediaType,
                ItemCount = entry.Items.Count
            });
        }

        return results.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<SharedPlaylistBrowseDto>> GetSharedPlaylistsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.Playlists == VisibilityScope.Nobody && viewerPrivacy.View.SmartPlaylists == VisibilityScope.Nobody)
            return [];

        var results = new List<SharedPlaylistBrowseDto>();

        if (viewerPrivacy.View.Playlists != VisibilityScope.Nobody)
        {
            var localOwners = await context.Users
                .AsNoTracking()
                .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null && u.Id != viewerUserId)
                .ToListAsync(cancellationToken);

            foreach (var owner in localOwners)
            {
                var ownerPrivacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
                if (ownerPrivacy.Share.Playlists == VisibilityScope.Nobody)
                    continue;

                if (!await visibilityEvaluator.CanViewAsync(
                    viewerUserId, owner.Id, FederationContentType.Playlists, ownerPrivacy.Share.Playlists, cancellationToken: cancellationToken))
                    continue;

                var playlists = await context.Playlists
                    .AsNoTracking()
                    .Include(p => p.Items)
                    .Where(p => p.UserId == owner.Id && p.VisibilityScope != VisibilityScope.Nobody)
                    .ToListAsync(cancellationToken);

                var avatarId = await GetAvatarPictureIdAsync(owner.Id, cancellationToken);
                var ownerIdentity = await ToLocalIdentityAsync(owner, avatarId, cancellationToken);

                foreach (var playlist in playlists)
                {
                    if (!await visibilityEvaluator.CanViewAsync(
                        viewerUserId,
                        owner.Id,
                        FederationContentType.Playlists,
                        playlist.VisibilityScope,
                        playlistId: playlist.Id,
                        cancellationToken: cancellationToken))
                        continue;

                    results.Add(new SharedPlaylistBrowseDto
                    {
                        Owner = ownerIdentity,
                        PlaylistId = playlist.Id,
                        Title = playlist.Title,
                        Description = playlist.Description,
                        MediaType = playlist.MediaType,
                        IsSmart = false,
                        ItemCount = playlist.Items.Count
                    });
                }
            }

            var federatedPlaylists = await consumerService.GetPlaylistsAsync(viewerUserId, cancellationToken);
            foreach (var entry in federatedPlaylists)
            {
                results.Add(new SharedPlaylistBrowseDto
                {
                    Owner = new SocialUserIdentityDto
                    {
                        IsFederated = true,
                        PeerServerId = entry.PeerServerId,
                        OriginUserId = entry.OriginUserId,
                        DisplayName = entry.AuthorName.Split(" @ ").FirstOrDefault() ?? entry.AuthorName,
                        PeerName = entry.PeerName
                    },
                    PlaylistId = entry.Playlist.Id,
                    Title = entry.Playlist.Title,
                    Description = entry.Playlist.Description,
                    MediaType = entry.Playlist.MediaType,
                    IsSmart = false,
                    ItemCount = entry.Items.Count
                });
            }
        }

        if (viewerPrivacy.View.SmartPlaylists != VisibilityScope.Nobody)
        {
            var localOwners = await context.Users
                .AsNoTracking()
                .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null && u.Id != viewerUserId)
                .ToListAsync(cancellationToken);

            foreach (var owner in localOwners)
            {
                var ownerPrivacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
                if (ownerPrivacy.Share.SmartPlaylists == VisibilityScope.Nobody)
                    continue;

                if (!await visibilityEvaluator.CanViewAsync(
                    viewerUserId, owner.Id, FederationContentType.SmartPlaylists, ownerPrivacy.Share.SmartPlaylists, cancellationToken: cancellationToken))
                    continue;

                var smartPlaylists = await context.Playlists
                    .OfType<SmartPlaylist>()
                    .AsNoTracking()
                    .Where(p => p.UserId == owner.Id && p.VisibilityScope != VisibilityScope.Nobody)
                    .ToListAsync(cancellationToken);

                var avatarId = await GetAvatarPictureIdAsync(owner.Id, cancellationToken);
                var ownerIdentity = await ToLocalIdentityAsync(owner, avatarId, cancellationToken);

                foreach (var playlist in smartPlaylists)
                {
                    if (!await visibilityEvaluator.CanViewAsync(
                        viewerUserId,
                        owner.Id,
                        FederationContentType.SmartPlaylists,
                        playlist.VisibilityScope,
                        playlistId: playlist.Id,
                        cancellationToken: cancellationToken))
                        continue;

                    results.Add(new SharedPlaylistBrowseDto
                    {
                        Owner = ownerIdentity,
                        PlaylistId = playlist.Id,
                        Title = playlist.Title,
                        Description = playlist.Description,
                        MediaType = playlist.MediaType,
                        IsSmart = true,
                        ItemCount = 0
                    });
                }
            }

            var federatedSmart = await consumerService.GetSmartPlaylistsAsync(viewerUserId, cancellationToken);
            foreach (var entry in federatedSmart)
            {
                results.Add(new SharedPlaylistBrowseDto
                {
                    Owner = new SocialUserIdentityDto
                    {
                        IsFederated = true,
                        PeerServerId = entry.PeerServerId,
                        OriginUserId = entry.OriginUserId,
                        DisplayName = entry.AuthorName.Split(" @ ").FirstOrDefault() ?? entry.AuthorName,
                        PeerName = entry.PeerName
                    },
                    PlaylistId = entry.Playlist.Id,
                    Title = entry.Playlist.Title,
                    Description = entry.Playlist.Description,
                    MediaType = entry.Playlist.MediaType,
                    IsSmart = true,
                    ItemCount = entry.Items.Count
                });
            }
        }

        return results.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<bool> IsLocalUserDiscoverableAsync(Guid viewerUserId, Guid ownerUserId, CancellationToken cancellationToken)
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

    private async Task<bool> IsDiscoverableLocalContentTypeAsync(
        Guid viewerUserId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope shareScope,
        CancellationToken cancellationToken)
    {
        if (shareScope == VisibilityScope.Nobody)
            return false;

        if (!await visibilityEvaluator.CanViewAsync(viewerUserId, ownerUserId, contentType, shareScope, cancellationToken: cancellationToken))
            return false;

        if (contentType == FederationContentType.PlaybackHistory)
            return true;

        return await HasLocalContentAsync(ownerUserId, contentType, cancellationToken);
    }

    private async Task<bool> HasLocalContentAsync(
        Guid ownerUserId,
        FederationContentType contentType,
        CancellationToken cancellationToken) =>
        contentType switch
        {
            FederationContentType.Reviews => await context.MediaReviews.AnyAsync(r => r.UserId == ownerUserId, cancellationToken),
            FederationContentType.Collections => await context.Collections.AnyAsync(c => c.UserId == ownerUserId && c.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.Playlists => await context.Playlists.AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.SmartPlaylists => await context.Playlists.OfType<SmartPlaylist>().AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.PlaybackHistory => await context.MediaPlaybackSessions.AnyAsync(s => s.UserId == ownerUserId, cancellationToken),
            _ => false
        };

    private static bool IsFederatedUserDiscoverableForViewer(
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId)
    {
        if (remoteUser.DiscoverableContentTypes.Count > 0)
        {
            foreach (var contentType in remoteUser.DiscoverableContentTypes)
            {
                if (SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId))
                    return true;
            }

            return false;
        }

        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId))
                return true;
        }

        return false;
    }

    private async Task<bool> CanViewFederatedUserAsync(
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId,
        CancellationToken cancellationToken)
    {
        var contentTypes = remoteUser.DiscoverableContentTypes.Count > 0
            ? remoteUser.DiscoverableContentTypes
            : Enum.GetValues<FederationContentType>();

        foreach (var contentType in contentTypes)
        {
            if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId))
                continue;

            if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
                continue;

            return true;
        }

        return false;
    }

    private async Task<SocialUserProfileVisibleSectionsDto> BuildLocalVisibleSectionsAsync(
        bool isSelf,
        Guid viewerUserId,
        Guid ownerUserId,
        FederationPrivacySettingsDto ownerPrivacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken) =>
        new()
        {
            Reviews = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Reviews, cancellationToken),
            PlaybackHistory = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.PlaybackHistory, cancellationToken),
            Collections = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Collections, cancellationToken),
            Playlists = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.Playlists, cancellationToken),
            SmartPlaylists = await IsLocalSectionVisibleAsync(isSelf, viewerUserId, ownerUserId, ownerPrivacy, viewerPrivacy, FederationContentType.SmartPlaylists, cancellationToken)
        };

    private async Task<bool> IsLocalSectionVisibleAsync(
        bool isSelf,
        Guid viewerUserId,
        Guid ownerUserId,
        FederationPrivacySettingsDto ownerPrivacy,
        FederationPrivacySettingsDto viewerPrivacy,
        FederationContentType contentType,
        CancellationToken cancellationToken)
    {
        if (isSelf)
            return true;

        if (!SocialViewVisibilityHelper.CanViewerSeeLocalContent(viewerPrivacy, contentType, ownerUserId))
            return false;

        var shareScope = SocialViewVisibilityHelper.GetShareScope(ownerPrivacy, contentType);
        if (shareScope == VisibilityScope.Nobody)
            return false;

        return await visibilityEvaluator.CanViewAsync(
            viewerUserId,
            ownerUserId,
            contentType,
            shareScope,
            cancellationToken: cancellationToken);
    }

    private async Task<SocialUserProfileVisibleSectionsDto> BuildFederatedVisibleSectionsAsync(
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId,
        CancellationToken cancellationToken) =>
        new()
        {
            Reviews = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Reviews, cancellationToken),
            PlaybackHistory = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.PlaybackHistory, cancellationToken),
            Collections = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Collections, cancellationToken),
            Playlists = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.Playlists, cancellationToken),
            SmartPlaylists = await IsFederatedSectionVisibleAsync(viewerPrivacy, remoteUser, peerServerId, FederationContentType.SmartPlaylists, cancellationToken)
        };

    private async Task<bool> IsFederatedSectionVisibleAsync(
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        Guid peerServerId,
        FederationContentType contentType,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(viewerPrivacy, contentType, remoteUser, peerServerId))
            return false;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
            return false;

        if (remoteUser.DiscoverableContentTypes.Count == 0)
            return true;

        return remoteUser.DiscoverableContentTypes.Contains(contentType);
    }

    private async Task<bool> HasAnyInboundSocialEnabledAsync(Guid peerServerId, CancellationToken cancellationToken)
    {
        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
                return true;
        }

        return false;
    }

    private async Task<Guid?> GetAvatarPictureIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static SocialUserMediaCardDto ToSocialMediaCard(FederatedSocialItemViewDto item) =>
        new()
        {
            Media = item.Media,
            Status = item.Status,
            LocalMediaId = item.LocalMediaId,
            RemoteIndexedFileId = item.RemoteIndexedFileId
        };

    private async Task EnrichCoverPictureIdsAsync(
        IList<SocialUserMediaCardDto> items,
        CancellationToken cancellationToken)
    {
        var mediaIds = items
            .Where(item => item.LocalMediaId is not null)
            .Select(item => item.LocalMediaId!.Value)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
            return;

        var coverPictureIds = await MediaCoverPictureResolver.GetCoverPictureIdsByMediaIdAsync(
            context,
            mediaIds,
            cancellationToken);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.LocalMediaId is not Guid mediaId)
                continue;

            if (!coverPictureIds.TryGetValue(mediaId, out var coverPictureId) || coverPictureId is null)
                continue;

            items[index] = item with { CoverPictureId = coverPictureId };
        }
    }

    private async Task<SocialUserIdentityDto> ToLocalIdentityAsync(
        User owner,
        Guid? avatarId,
        CancellationToken cancellationToken) =>
        new()
        {
            IsFederated = false,
            LocalUserId = owner.Id,
            DisplayName = await LocalUserDisplayNameHelper.ResolveAsync(identityService, owner, cancellationToken),
            AvatarPictureId = avatarId
        };

    private static SocialUserIdentityDto FederatedOwnerFromView(Guid peerServerId, Guid originUserId, string peerName, string authorName) =>
        new()
        {
            IsFederated = true,
            PeerServerId = peerServerId,
            OriginUserId = originUserId,
            DisplayName = authorName.Split(" @ ").FirstOrDefault() ?? authorName,
            PeerName = peerName
        };

    private async Task<IReadOnlyList<SocialUserReviewViewDto>> LoadLocalReviewsAsync(
        Guid ownerUserId,
        Guid viewerUserId,
        bool isSelf,
        FederationPrivacySettingsDto privacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken)
    {
        if (!isSelf && !SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                viewerPrivacy,
                FederationContentType.Reviews,
                ownerUserId))
            return [];

        if (!isSelf && (privacy.Share.Reviews == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, ownerUserId, FederationContentType.Reviews, privacy.Share.Reviews, cancellationToken: cancellationToken)))
            return [];

        var reviews = await context.MediaReviews
            .AsNoTracking()
            .IncludeReviewMediaDetails()
            .Where(r => r.UserId == ownerUserId)
            .OrderByDescending(r => r.Created)
            .Take(RecentLimit)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new SocialUserReviewViewDto
        {
            Id = r.Id,
            Text = r.Text,
            Emoji = r.Emoji,
            Rating = (int)(r.UserRating?.Value ?? 0),
            Created = r.Created,
            Media = r.Media!.ToSocialUserMediaCard(FederatedSocialItemStatus.ResolvedLocal)
        }).ToList();
    }

    private async Task<IReadOnlyList<SocialUserPlaybackViewDto>> LoadLocalPlaybackAsync(
        Guid ownerUserId,
        Guid viewerUserId,
        bool isSelf,
        FederationPrivacySettingsDto privacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken)
    {
        if (!isSelf && !SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                viewerPrivacy,
                FederationContentType.PlaybackHistory,
                ownerUserId))
            return [];

        if (!isSelf && (privacy.Share.PlaybackHistory == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, ownerUserId, FederationContentType.PlaybackHistory, privacy.Share.PlaybackHistory, cancellationToken: cancellationToken)))
            return [];

        var sessions = await context.MediaPlaybackSessions
            .AsNoTracking()
            .Include(s => s.Media)
            .Where(s => s.UserId == ownerUserId)
            .OrderByDescending(s => s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt)
            .Take(RecentLimit)
            .ToListAsync(cancellationToken);

        var mediaIds = sessions.Select(s => s.MediaId).Distinct().ToList();
        var mediaHrefById = await BuildMediaHrefByIdAsync(mediaIds, cancellationToken);
        var coverPictureIds = await MediaCoverPictureResolver.GetCoverPictureIdsByMediaIdAsync(
            context,
            mediaIds,
            cancellationToken);

        return sessions.Select(s => new SocialUserPlaybackViewDto
        {
            LocalMediaId = s.MediaId,
            MediaTitle = s.Media?.Title,
            MediaType = s.Media?.Type,
            MediaHref = mediaHrefById.GetValueOrDefault(s.MediaId),
            ImageUrl = MediaCoverPictureResolver.ToSmallPictureUrl(coverPictureIds.GetValueOrDefault(s.MediaId)),
            EndedAt = s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt
        }).ToList();
    }

    private async Task<Dictionary<Guid, string?>> BuildMediaHrefByIdAsync(
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
            return [];

        var episodeNavById = await context.Medias.OfType<SerieEpisode>()
            .Where(e => mediaIds.Contains(e.Id))
            .Select(e => new { e.Id, e.SerieId, SeasonNumber = e.Season.SeasonNumber, e.EpisodeNumber })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var seasonNavById = await context.Medias.OfType<SerieSeason>()
            .Where(s => mediaIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SerieId, s.SeasonNumber })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var trackNavById = await context.Medias.OfType<MusicTrack>()
            .Where(t => mediaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.AlbumId })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Type })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var results = new Dictionary<Guid, string?>(mediaIds.Count);
        foreach (var mediaId in mediaIds)
        {
            if (!medias.TryGetValue(mediaId, out var media))
                continue;

            episodeNavById.TryGetValue(mediaId, out var episodeNav);
            seasonNavById.TryGetValue(mediaId, out var seasonNav);
            trackNavById.TryGetValue(mediaId, out var trackNav);

            results[mediaId] = MediaPageUrls.Build(
                media.Type,
                mediaId,
                episodeNav?.SerieId ?? seasonNav?.SerieId,
                episodeNav?.SeasonNumber ?? seasonNav?.SeasonNumber,
                episodeNav?.EpisodeNumber,
                trackNav?.AlbumId);
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserCollectionCardDto>> LoadLocalCollectionsAsync(
        User owner,
        Guid viewerUserId,
        bool isSelf,
        FederationPrivacySettingsDto privacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken)
    {
        if (!isSelf && !SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                viewerPrivacy,
                FederationContentType.Collections,
                owner.Id))
            return [];

        if (!isSelf && (privacy.Share.Collections == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, owner.Id, FederationContentType.Collections, privacy.Share.Collections, cancellationToken: cancellationToken)))
            return [];

        var collections = await context.Collections
            .AsNoTracking()
            .Include(c => c.CoverPicture)
            .Include(c => c.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m!.ExternalIds)
            .Where(c => c.UserId == owner.Id && (isSelf || c.VisibilityScope != VisibilityScope.Nobody))
            .ToListAsync(cancellationToken);

        var results = new List<SocialUserCollectionCardDto>();
        foreach (var collection in collections)
        {
            if (!isSelf && !await visibilityEvaluator.CanViewAsync(
                viewerUserId, owner.Id, FederationContentType.Collections, collection.VisibilityScope, collectionId: collection.Id, cancellationToken: cancellationToken))
                continue;

            var previewItems = collection.Items
                .OrderBy(i => i.Order)
                .Take(PreviewItemLimit)
                .Select(i => new SocialUserMediaCardDto
                {
                    Media = i.Media!.ToFederatedMediaRef(),
                    Status = FederatedSocialItemStatus.ResolvedLocal,
                    LocalMediaId = i.MediaId
                })
                .ToList();

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserCollectionCardDto
            {
                Id = collection.Id,
                Title = collection.Title,
                Description = collection.Description,
                MediaType = collection.MediaType,
                CoverPictureId = collection.CoverPicture?.Id,
                ItemCount = collection.Items.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserPlaylistCardDto>> LoadLocalPlaylistsAsync(
        User owner,
        Guid viewerUserId,
        bool isSelf,
        FederationPrivacySettingsDto privacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken)
    {
        if (!isSelf && !SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                viewerPrivacy,
                FederationContentType.Playlists,
                owner.Id))
            return [];

        if (!isSelf && (privacy.Share.Playlists == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, owner.Id, FederationContentType.Playlists, privacy.Share.Playlists, cancellationToken: cancellationToken)))
            return [];

        var playlists = await context.Playlists
            .AsNoTracking()
            .Include(p => p.CoverPicture)
            .Include(p => p.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m!.ExternalIds)
            .Where(p => p.UserId == owner.Id && (isSelf || p.VisibilityScope != VisibilityScope.Nobody))
            .ToListAsync(cancellationToken);

        var results = new List<SocialUserPlaylistCardDto>();
        foreach (var playlist in playlists)
        {
            if (!isSelf && !await visibilityEvaluator.CanViewAsync(
                viewerUserId, owner.Id, FederationContentType.Playlists, playlist.VisibilityScope, playlistId: playlist.Id, cancellationToken: cancellationToken))
                continue;

            var previewItems = playlist.Items
                .OrderBy(i => i.Order)
                .Take(PreviewItemLimit)
                .Select(i => new SocialUserMediaCardDto
                {
                    Media = i.Media!.ToFederatedMediaRef(),
                    Status = FederatedSocialItemStatus.ResolvedLocal,
                    LocalMediaId = i.MediaId
                })
                .ToList();

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserPlaylistCardDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                IsSmart = false,
                CoverPictureId = playlist.CoverPicture?.Id,
                ItemCount = playlist.Items.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserPlaylistCardDto>> LoadLocalSmartPlaylistsAsync(
        User owner,
        Guid viewerUserId,
        bool isSelf,
        FederationPrivacySettingsDto privacy,
        FederationPrivacySettingsDto viewerPrivacy,
        CancellationToken cancellationToken)
    {
        if (!isSelf && !SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                viewerPrivacy,
                FederationContentType.SmartPlaylists,
                owner.Id))
            return [];

        if (!isSelf && (privacy.Share.SmartPlaylists == VisibilityScope.Nobody
            || !await visibilityEvaluator.CanViewAsync(viewerUserId, owner.Id, FederationContentType.SmartPlaylists, privacy.Share.SmartPlaylists, cancellationToken: cancellationToken)))
            return [];

        var playlists = await context.Playlists
            .OfType<SmartPlaylist>()
            .AsNoTracking()
            .Include(p => p.CoverPicture)
            .Where(p => p.UserId == owner.Id && (isSelf || p.VisibilityScope != VisibilityScope.Nobody))
            .ToListAsync(cancellationToken);

        var results = new List<SocialUserPlaylistCardDto>();
        foreach (var playlist in playlists)
        {
            if (!isSelf && !await visibilityEvaluator.CanViewAsync(
                viewerUserId, owner.Id, FederationContentType.SmartPlaylists, playlist.VisibilityScope, playlistId: playlist.Id, cancellationToken: cancellationToken))
                continue;

            var query = context.Medias.Where(m => m.IndexedFiles.Any()).AsNoTracking();
            query = SmartPlaylistEvaluator.ApplyRules(query, playlist, viewerUserId);
            var mediaIds = await query.Select(m => m.Id).Take(PreviewItemLimit).ToListAsync(cancellationToken);

            var previewItems = new List<SocialUserMediaCardDto>();
            foreach (var mediaId in mediaIds)
            {
                var media = await context.Medias.AsNoTracking().Include(m => m.ExternalIds).FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
                if (media is null)
                    continue;

                previewItems.Add(new SocialUserMediaCardDto
                {
                    Media = media.ToFederatedMediaRef(),
                    Status = FederatedSocialItemStatus.ResolvedLocal,
                    LocalMediaId = media.Id
                });
            }

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserPlaylistCardDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                IsSmart = true,
                CoverPictureId = playlist.CoverPicture?.Id,
                ItemCount = previewItems.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserReviewViewDto>> LoadFederatedReviewsAsync(
        PeerServer peer,
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        string token,
        string assertion,
        Guid originUserId,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(
                viewerPrivacy,
                FederationContentType.Reviews,
                remoteUser,
                peer.Id))
            return [];

        var reviews = await peerClient.GetRemoteSocialReviewsAsync(peer.BaseUrl, token, assertion, originUserId, cancellationToken);
        var results = new List<SocialUserReviewViewDto>();

        foreach (var review in reviews.Take(RecentLimit))
        {
            var resolution = await mediaResolver.ResolveAsync(peer.Id, review.Media, cancellationToken);
            results.Add(new SocialUserReviewViewDto
            {
                Id = review.Id,
                Text = review.Text,
                Emoji = review.Emoji,
                Rating = (int)review.Rating,
                Created = review.Created,
                Media = ToSocialMediaCard(FederationSocialConsumerHelper.ToItemView(review.Media, resolution))
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserPlaybackViewDto>> LoadFederatedPlaybackAsync(
        PeerServer peer,
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        string token,
        string assertion,
        Guid originUserId,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(
                viewerPrivacy,
                FederationContentType.PlaybackHistory,
                remoteUser,
                peer.Id))
            return [];

        var entries = await peerClient.GetRemoteSocialPlaybackHistoryAsync(peer.BaseUrl, token, assertion, originUserId, cancellationToken);
        var results = new List<SocialUserPlaybackViewDto>();

        foreach (var entry in entries.Take(RecentLimit))
        {
            var remoteFile = await context.RemoteIndexedFiles
                .AsNoTracking()
                .Include(f => f.Media)
                .FirstOrDefaultAsync(f => f.PeerServerId == peer.Id && f.RemoteMediaId == entry.MediaId, cancellationToken);

            results.Add(new SocialUserPlaybackViewDto
            {
                LocalMediaId = remoteFile?.MediaId,
                MediaTitle = entry.MediaTitle,
                MediaType = remoteFile?.Media?.Type,
                EndedAt = entry.EndedAt
            });
        }

        var localMediaIds = results
            .Where(r => r.LocalMediaId is Guid id)
            .Select(r => r.LocalMediaId!.Value)
            .Distinct()
            .ToList();
        var mediaHrefById = await BuildMediaHrefByIdAsync(localMediaIds, cancellationToken);
        var coverPictureIds = await MediaCoverPictureResolver.GetCoverPictureIdsByMediaIdAsync(
            context,
            localMediaIds,
            cancellationToken);

        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            if (item.LocalMediaId is not Guid mediaId)
                continue;

            results[i] = item with
            {
                MediaHref = mediaHrefById.GetValueOrDefault(mediaId),
                ImageUrl = MediaCoverPictureResolver.ToSmallPictureUrl(coverPictureIds.GetValueOrDefault(mediaId))
            };
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserCollectionCardDto>> LoadFederatedCollectionsAsync(
        PeerServer peer,
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        string token,
        string assertion,
        Guid originUserId,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(
                viewerPrivacy,
                FederationContentType.Collections,
                remoteUser,
                peer.Id))
            return [];

        var collections = await peerClient.GetRemoteSocialCollectionsAsync(peer.BaseUrl, token, assertion, originUserId, cancellationToken);
        var results = new List<SocialUserCollectionCardDto>();

        foreach (var collection in collections)
        {
            var previewItems = new List<SocialUserMediaCardDto>();
            foreach (var item in collection.Items.OrderBy(i => i.Order).Take(PreviewItemLimit))
            {
                var resolution = await mediaResolver.ResolveAsync(peer.Id, item.Media, cancellationToken);
                previewItems.Add(ToSocialMediaCard(FederationSocialConsumerHelper.ToItemView(item.Media, resolution)));
            }

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserCollectionCardDto
            {
                Id = collection.Id,
                Title = collection.Title,
                Description = collection.Description,
                MediaType = collection.MediaType,
                ItemCount = collection.Items.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserPlaylistCardDto>> LoadFederatedPlaylistsAsync(
        PeerServer peer,
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        string token,
        string assertion,
        Guid originUserId,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(
                viewerPrivacy,
                FederationContentType.Playlists,
                remoteUser,
                peer.Id))
            return [];

        var playlists = await peerClient.GetRemoteSocialPlaylistsAsync(peer.BaseUrl, token, assertion, originUserId, cancellationToken);
        var results = new List<SocialUserPlaylistCardDto>();

        foreach (var playlist in playlists)
        {
            var previewItems = new List<SocialUserMediaCardDto>();
            foreach (var item in playlist.Items.OrderBy(i => i.Order).Take(PreviewItemLimit))
            {
                var resolution = await mediaResolver.ResolveAsync(peer.Id, item.Media, cancellationToken);
                previewItems.Add(ToSocialMediaCard(FederationSocialConsumerHelper.ToItemView(item.Media, resolution)));
            }

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserPlaylistCardDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                IsSmart = false,
                ItemCount = playlist.Items.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<SocialUserPlaylistCardDto>> LoadFederatedSmartPlaylistsAsync(
        PeerServer peer,
        Guid viewerUserId,
        FederationPrivacySettingsDto viewerPrivacy,
        FederatedUserRef remoteUser,
        string token,
        string assertion,
        Guid originUserId,
        CancellationToken cancellationToken)
    {
        if (!SocialViewVisibilityHelper.CanViewerSeeFederatedContent(
                viewerPrivacy,
                FederationContentType.SmartPlaylists,
                remoteUser,
                peer.Id))
            return [];

        var playlists = await peerClient.GetRemoteSocialSmartPlaylistsAsync(peer.BaseUrl, token, assertion, originUserId, cancellationToken);
        var results = new List<SocialUserPlaylistCardDto>();

        foreach (var playlist in playlists)
        {
            var smartPlaylist = new SmartPlaylist
            {
                Title = playlist.Title,
                MediaType = playlist.MediaType,
                UserId = viewerUserId,
                RuleFilter = playlist.RuleFilter.ToRuleGroup(),
                Limit = playlist.Limit,
                OrderBy = playlist.OrderBy,
                OrderDescending = playlist.OrderDescending
            };

            var query = context.Medias.Where(m => m.IndexedFiles.Any()).AsNoTracking();
            query = SmartPlaylistEvaluator.ApplyRules(query, smartPlaylist, viewerUserId);
            var mediaIds = await query.Select(m => m.Id).Take(PreviewItemLimit).ToListAsync(cancellationToken);

            var previewItems = new List<SocialUserMediaCardDto>();
            foreach (var mediaId in mediaIds)
            {
                var media = await context.Medias.AsNoTracking().Include(m => m.ExternalIds).FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
                if (media is null)
                    continue;

                var mediaRef = media.ToFederatedMediaRef();
                var resolution = await mediaResolver.ResolveAsync(peer.Id, mediaRef, cancellationToken);
                previewItems.Add(ToSocialMediaCard(FederationSocialConsumerHelper.ToItemView(mediaRef, resolution)));
            }

            await EnrichCoverPictureIdsAsync(previewItems, cancellationToken);

            results.Add(new SocialUserPlaylistCardDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                IsSmart = true,
                ItemCount = previewItems.Count,
                PreviewItems = previewItems
            });
        }

        return results;
    }

    private static bool HasSocialViewEnabled(FederationContentVisibilityDto view) =>
        view.Reviews != VisibilityScope.Nobody
        || view.Collections != VisibilityScope.Nobody
        || view.Playlists != VisibilityScope.Nobody
        || view.SmartPlaylists != VisibilityScope.Nobody
        || view.PlaybackHistory != VisibilityScope.Nobody;
}
