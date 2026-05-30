using K7.Shared.Dtos.Entities;

namespace K7.Shared.Interfaces;

public interface IFederationService
{
    Task<List<PeerServerDto>> GetPeerServersAsync(CancellationToken cancellationToken = default);
    Task RequestPeerAsync(string remoteUrl, CancellationToken cancellationToken = default);
    Task AcceptPeerAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task RevokePeerAsync(Guid peerId, CancellationToken cancellationToken = default);
    Task SyncPeerAsync(Guid peerId, CancellationToken cancellationToken = default);
}
