using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SmartPlaylists.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Services;

public interface IFederationSocialConsumerService
{
    Task<IReadOnlyList<FederatedCollectionViewDto>> GetCollectionsAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedPlaylistViewDto>> GetPlaylistsAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedSmartPlaylistViewDto>> GetSmartPlaylistsAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FederatedPlaybackHistoryViewDto>> GetPlaybackHistoryAsync(Guid viewerUserId, CancellationToken cancellationToken = default);
}

public class FederationSocialConsumerService(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IFederationViewerAssertionService assertionService,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator,
    IFederatedMediaResolver mediaResolver)
    : IFederationSocialConsumerService
{
    public async Task<IReadOnlyList<FederatedCollectionViewDto>> GetCollectionsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.Collections == VisibilityScope.Nobody)
            return [];

        var results = new List<FederatedCollectionViewDto>();
        await foreach (var remoteUser in EnumerateRemoteUsersAsync(
            viewerUserId, FederationContentType.Collections, viewerPrivacy, cancellationToken))
        {
            var collections = await peerClient.GetRemoteSocialCollectionsAsync(
                remoteUser.Peer.BaseUrl,
                remoteUser.Token,
                remoteUser.Assertion,
                remoteUser.User.OriginUserId,
                cancellationToken);

            foreach (var collection in collections)
            {
                var items = new List<FederatedSocialItemViewDto>();
                foreach (var item in collection.Items)
                {
                    var resolution = await mediaResolver.ResolveAsync(remoteUser.Peer.Id, item.Media, cancellationToken);
                    items.Add(FederationSocialConsumerHelper.ToItemView(item.Media, resolution));
                }

                results.Add(new FederatedCollectionViewDto
                {
                    PeerServerId = remoteUser.Peer.Id,
                    PeerName = remoteUser.Peer.Name,
                    OriginUserId = remoteUser.User.OriginUserId,
                    AuthorName = $"{remoteUser.User.DisplayName} @ {remoteUser.Peer.Name}",
                    Collection = collection,
                    Items = items
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<FederatedPlaylistViewDto>> GetPlaylistsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.Playlists == VisibilityScope.Nobody)
            return [];

        var results = new List<FederatedPlaylistViewDto>();
        await foreach (var remoteUser in EnumerateRemoteUsersAsync(
            viewerUserId, FederationContentType.Playlists, viewerPrivacy, cancellationToken))
        {
            var playlists = await peerClient.GetRemoteSocialPlaylistsAsync(
                remoteUser.Peer.BaseUrl,
                remoteUser.Token,
                remoteUser.Assertion,
                remoteUser.User.OriginUserId,
                cancellationToken);

            foreach (var playlist in playlists)
            {
                var items = new List<FederatedSocialItemViewDto>();
                foreach (var item in playlist.Items)
                {
                    var resolution = await mediaResolver.ResolveAsync(remoteUser.Peer.Id, item.Media, cancellationToken);
                    items.Add(FederationSocialConsumerHelper.ToItemView(item.Media, resolution));
                }

                results.Add(new FederatedPlaylistViewDto
                {
                    PeerServerId = remoteUser.Peer.Id,
                    PeerName = remoteUser.Peer.Name,
                    OriginUserId = remoteUser.User.OriginUserId,
                    AuthorName = $"{remoteUser.User.DisplayName} @ {remoteUser.Peer.Name}",
                    Playlist = playlist,
                    Items = items
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<FederatedSmartPlaylistViewDto>> GetSmartPlaylistsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.SmartPlaylists == VisibilityScope.Nobody)
            return [];

        var results = new List<FederatedSmartPlaylistViewDto>();
        await foreach (var remoteUser in EnumerateRemoteUsersAsync(
            viewerUserId, FederationContentType.SmartPlaylists, viewerPrivacy, cancellationToken))
        {
            var playlists = await peerClient.GetRemoteSocialSmartPlaylistsAsync(
                remoteUser.Peer.BaseUrl,
                remoteUser.Token,
                remoteUser.Assertion,
                remoteUser.User.OriginUserId,
                cancellationToken);

            foreach (var playlist in playlists)
            {
                var items = await EvaluateSmartPlaylistItemsAsync(playlist, viewerUserId, remoteUser.Peer.Id, cancellationToken);
                results.Add(new FederatedSmartPlaylistViewDto
                {
                    PeerServerId = remoteUser.Peer.Id,
                    PeerName = remoteUser.Peer.Name,
                    OriginUserId = remoteUser.User.OriginUserId,
                    AuthorName = $"{remoteUser.User.DisplayName} @ {remoteUser.Peer.Name}",
                    Playlist = playlist,
                    Items = items
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<FederatedPlaybackHistoryViewDto>> GetPlaybackHistoryAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.PlaybackHistory == VisibilityScope.Nobody)
            return [];

        var peers = await FederationSocialConsumerHelper.GetActiveOutboundPeersAsync(context, cancellationToken);
        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);
        var results = new List<FederatedPlaybackHistoryViewDto>();

        foreach (var peer in peers)
        {
            if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.PlaybackHistory, outbound: false, peer.Id, cancellationToken))
                continue;

            var shareEnabled = await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peer.Id
                    && a.Direction == ShareDirection.Inbound
                    && a.IsEnabled
                    && a.SharePlaybackHistory, cancellationToken);

            if (!shareEnabled)
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

            var entries = await peerClient.GetRemotePlaybackHistoryAsync(peer.BaseUrl, token, assertion, cancellationToken);
            foreach (var entry in entries)
            {
                var remoteFile = await context.RemoteIndexedFiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.PeerServerId == peer.Id && f.RemoteFileId == entry.FileId, cancellationToken);

                results.Add(new FederatedPlaybackHistoryViewDto
                {
                    PeerServerId = peer.Id,
                    PeerName = peer.Name,
                    UserDisplayName = entry.UserDisplayName,
                    LocalMediaId = remoteFile?.MediaId,
                    MediaTitle = remoteFile?.Name,
                    Position = TimeSpan.FromSeconds(entry.Position),
                    EndedAt = entry.EndedAt
                });
            }
        }

        return results.OrderByDescending(e => e.EndedAt).Take(100).ToList();
    }

    private async IAsyncEnumerable<RemoteSocialUserContext> EnumerateRemoteUsersAsync(
        Guid viewerUserId,
        FederationContentType contentType,
        FederationPrivacySettingsDto viewerPrivacy,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var viewScope = contentType switch
        {
            FederationContentType.Reviews => viewerPrivacy.View.Reviews,
            FederationContentType.Collections => viewerPrivacy.View.Collections,
            FederationContentType.Playlists => viewerPrivacy.View.Playlists,
            FederationContentType.SmartPlaylists => viewerPrivacy.View.SmartPlaylists,
            FederationContentType.PlaybackHistory => viewerPrivacy.View.PlaybackHistory,
            _ => VisibilityScope.Nobody
        };

        if (viewScope == VisibilityScope.Nobody || viewScope == VisibilityScope.LocalServer)
            yield break;

        var peers = await FederationSocialConsumerHelper.GetActiveOutboundPeersAsync(context, cancellationToken);
        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);

        foreach (var peer in peers)
        {
            if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                contentType, outbound: false, peer.Id, cancellationToken))
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
                if (viewScope == VisibilityScope.SpecificPeople
                    && !FederationSocialConsumerHelper.MatchesViewGrants(
                        contentType, viewScope, viewerPrivacy.View.Grants, remoteUser, peer.Id))
                    continue;

                yield return new RemoteSocialUserContext(peer, token, assertion, remoteUser);
            }
        }
    }

    private async Task<IReadOnlyList<FederatedSocialItemViewDto>> EvaluateSmartPlaylistItemsAsync(
        FederatedSmartPlaylistDto playlist,
        Guid viewerUserId,
        Guid peerServerId,
        CancellationToken cancellationToken)
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
        var mediaIds = await query.Select(m => m.Id).ToListAsync(cancellationToken);

        var items = new List<FederatedSocialItemViewDto>();
        foreach (var mediaId in mediaIds)
        {
            var media = await context.Medias
                .AsNoTracking()
                .Include(m => m.ExternalIds)
                .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

            if (media is null)
                continue;

            var mediaRef = media.ToFederatedMediaRef();
            var resolution = await mediaResolver.ResolveAsync(peerServerId, mediaRef, cancellationToken);
            items.Add(FederationSocialConsumerHelper.ToItemView(mediaRef, resolution));
        }

        return items;
    }

    private sealed record RemoteSocialUserContext(
        PeerServer Peer,
        string Token,
        string Assertion,
        FederatedUserRef User);
}
