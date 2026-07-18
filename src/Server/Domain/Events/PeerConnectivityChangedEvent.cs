using K7.Server.Domain.Entities.Federation;

namespace K7.Server.Domain.Events;

public class PeerConnectivityChangedEvent : BaseEvent
{
    public PeerConnectivityChangedEvent(PeerServer peer, bool succeeded, bool? previousSucceeded)
    {
        Peer = peer;
        Succeeded = succeeded;
        PreviousSucceeded = previousSucceeded;
    }

    public PeerServer Peer { get; }
    public bool Succeeded { get; }
    public bool? PreviousSucceeded { get; }
}
