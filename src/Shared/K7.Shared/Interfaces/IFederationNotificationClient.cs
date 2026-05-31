using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Interfaces;

public interface IFederationNotificationClient
{
    Task ReceivePeerStateChanged(Guid peerId, PeerStatus newStatus);
    Task ReceivePeerRequestReceived(PeerRequestDto request);
    Task ReceivePeerTestResult(Guid peerId, bool reachable);
}
