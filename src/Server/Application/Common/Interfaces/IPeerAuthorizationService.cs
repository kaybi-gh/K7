using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Common.Interfaces;

public interface IPeerAuthorizationService
{
    const string ViewerAssertionHeader = "X-K7-Federation-Viewer";

    Task<PeerServer?> ResolveInboundPeerAsync(string? clientId, CancellationToken cancellationToken = default);

    Task<PeerServer> RequireInboundPeerAsync(string? clientId, CancellationToken cancellationToken = default);

    Task<(PeerServer Peer, FederatedUserRef Viewer)?> ResolvePeerWithViewerAsync(
        string? clientId,
        string? viewerAssertion,
        CancellationToken cancellationToken = default);

    Task<PeerServer?> ResolvePeerByBaseUrlAsync(string baseUrl, PeerStatus requiredStatus, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetOutboundSharedLibraryIdsAsync(Guid peerServerId, CancellationToken cancellationToken = default);

    Task RequireLibrarySharedWithPeerAsync(Guid peerServerId, Guid libraryId, CancellationToken cancellationToken = default);

    Task<bool> IsMediaAccessibleToPeerAsync(Guid peerServerId, Guid mediaId, CancellationToken cancellationToken = default);

    Task<IndexedFile> RequireFileAccessibleToPeerAsync(Guid peerServerId, Guid indexedFileId, CancellationToken cancellationToken = default);

    Task<(PeerServer Peer, string Token)?> AuthenticateOutboundAsync(Guid peerServerId, CancellationToken cancellationToken = default);

    Task EnsureConcurrentStreamQuotaAsync(Guid peerServerId, Guid libraryId, CancellationToken cancellationToken = default);
}
