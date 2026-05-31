using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Interfaces;

public interface IFederationNotifier
{
    Task NotifyPeerStateChangedAsync(Guid peerId, PeerStatus newStatus, CancellationToken cancellationToken = default);
    Task NotifyPeerRequestReceivedAsync(PeerRequestDto request, CancellationToken cancellationToken = default);
    Task NotifyPeerTestResultAsync(Guid peerId, bool reachable, CancellationToken cancellationToken = default);
}
