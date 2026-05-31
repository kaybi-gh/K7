using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IFederationService
{
    Task<List<PeerServerDto>> GetPeerServersAsync(CancellationToken cancellationToken = default);
    Task<List<PeerRequestDto>> GetPeerRequestsAsync(CancellationToken cancellationToken = default);
    Task RequestPeerAsync(string remoteUrl, CancellationToken cancellationToken = default);
    Task AcceptPeerAsync(Guid requestId, IReadOnlyList<Guid> sharedLibraryIds, bool autoShareNewLibraries = false, CancellationToken cancellationToken = default);
    Task RejectPeerAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task UpdatePeerAsync(Guid peerId, UpdatePeerRequest request, CancellationToken cancellationToken = default);
    Task<bool> TestPeerAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task RevokePeerAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task SyncPeerAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task<List<PeerShareAgreementDto>> DiscoverPeerLibrariesAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task<IndexedFileDto?> GetRemoteFileDetailsAsync(Guid remoteFileId, CancellationToken cancellationToken = default);
}
