using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public class PeerAuthorizationService(
    IApplicationDbContext context,
    IFederationViewerAssertionService assertionService,
    IPeerClient peerClient) : IPeerAuthorizationService
{
    public async Task<PeerServer?> ResolveInboundPeerAsync(string? clientId, CancellationToken cancellationToken = default)
    {
        if (clientId is null)
            return null;

        return await context.PeerServers
            .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);
    }

    public async Task<PeerServer> RequireInboundPeerAsync(string? clientId, CancellationToken cancellationToken = default)
    {
        var peer = await ResolveInboundPeerAsync(clientId, cancellationToken);
        if (peer is null)
            throw new ForbiddenAccessException();

        return peer;
    }

    public async Task<(PeerServer Peer, FederatedUserRef Viewer)?> ResolvePeerWithViewerAsync(
        string? clientId,
        string? viewerAssertion,
        CancellationToken cancellationToken = default)
    {
        if (clientId is null)
            throw new ForbiddenAccessException();

        var peer = await ResolveInboundPeerAsync(clientId, cancellationToken);
        if (peer is null)
            throw new ForbiddenAccessException();

        if (string.IsNullOrWhiteSpace(peer.FederationAssertionSecret))
            throw new UnauthorizedAccessException();

        var viewer = assertionService.ValidateAssertion(viewerAssertion, peer.FederationAssertionSecret);
        if (viewer is null)
            throw new UnauthorizedAccessException();

        return (peer, viewer);
    }

    public async Task<PeerServer?> ResolvePeerByBaseUrlAsync(
        string baseUrl,
        PeerStatus requiredStatus,
        CancellationToken cancellationToken = default)
    {
        var normalizedUrl = baseUrl.TrimEnd('/');

        return await context.PeerServers
            .FirstOrDefaultAsync(p => p.BaseUrl == normalizedUrl && p.Status == requiredStatus, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetOutboundSharedLibraryIdsAsync(
        Guid peerServerId,
        CancellationToken cancellationToken = default) =>
        await context.PeerShareAgreements
            .Where(a => a.PeerServerId == peerServerId && a.Direction == ShareDirection.Outbound && a.IsEnabled)
            .Select(a => a.LibraryId)
            .ToListAsync(cancellationToken);

    public async Task RequireLibrarySharedWithPeerAsync(
        Guid peerServerId,
        Guid libraryId,
        CancellationToken cancellationToken = default)
    {
        var isShared = await context.PeerShareAgreements
            .AnyAsync(a => a.PeerServerId == peerServerId
                && a.LibraryId == libraryId
                && a.Direction == ShareDirection.Outbound
                && a.IsEnabled, cancellationToken);

        if (!isShared)
            throw new ForbiddenAccessException();
    }

    public async Task<bool> IsMediaAccessibleToPeerAsync(
        Guid peerServerId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.PeerServerId == null, cancellationToken);

        if (media is null)
            return false;

        if (media is MusicAlbum albumMedia)
        {
            var trackIds = await context.Medias.OfType<MusicTrack>()
                .Where(t => t.AlbumId == albumMedia.Id)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var relatedMediaIds = trackIds.Append(mediaId).ToList();
            return await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peerServerId
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled
                    && context.MediaLibraryAvailabilities.Any(av =>
                        av.LibraryId == a.LibraryId && relatedMediaIds.Contains(av.MediaId)), cancellationToken);
        }

        if (media is Serie serieMedia)
        {
            var episodeIds = await context.Medias.OfType<SerieEpisode>()
                .Where(e => e.SerieId == serieMedia.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            var relatedMediaIds = episodeIds.Append(mediaId).ToList();
            return await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peerServerId
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled
                    && context.MediaLibraryAvailabilities.Any(av =>
                        av.LibraryId == a.LibraryId && relatedMediaIds.Contains(av.MediaId)), cancellationToken);
        }

        return await context.PeerShareAgreements
            .AnyAsync(a => a.PeerServerId == peerServerId
                && a.Direction == ShareDirection.Outbound
                && a.IsEnabled
                && context.MediaLibraryAvailabilities.Any(av =>
                    av.LibraryId == a.LibraryId && av.MediaId == mediaId), cancellationToken);
    }

    public async Task<IndexedFile> RequireFileAccessibleToPeerAsync(
        Guid peerServerId,
        Guid indexedFileId,
        CancellationToken cancellationToken = default)
    {
        var sharedLibraryIds = await GetOutboundSharedLibraryIdsAsync(peerServerId, cancellationToken);

        var indexedFile = await context.IndexedFiles
            .Include(f => f.FileMetadata)
            .FirstOrDefaultAsync(f => f.Id == indexedFileId && sharedLibraryIds.Contains(f.LibraryId), cancellationToken);

        if (indexedFile is null)
            throw new NotFoundException(indexedFileId.ToString(), nameof(IndexedFile));

        return indexedFile;
    }

    public async Task<(PeerServer Peer, string Token)?> AuthenticateOutboundAsync(
        Guid peerServerId,
        CancellationToken cancellationToken = default)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == peerServerId, cancellationToken);

        if (peer is null || peer.Status != PeerStatus.Active)
            return null;

        var token = await peerClient.GetAccessTokenAsync(
            peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);

        if (token is null)
            return null;

        return (peer, token);
    }

    public async Task EnsureConcurrentStreamQuotaAsync(
        Guid peerServerId,
        Guid libraryId,
        CancellationToken cancellationToken = default)
    {
        var agreement = await context.PeerShareAgreements
            .FirstOrDefaultAsync(a => a.PeerServerId == peerServerId
                && a.LibraryId == libraryId
                && a.Direction == ShareDirection.Outbound, cancellationToken);

        if (agreement?.MaxConcurrentStreams is null)
            return;

        var activeStreams = await context.StreamSessions
            .CountAsync(s => s.PeerServerId == peerServerId && s.EndedAt == null, cancellationToken);

        if (activeStreams >= agreement.MaxConcurrentStreams)
            throw new ConcurrentStreamQuotaExceededException();
    }
}
