using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Federation.Queries.PingFederationPeer;

public record PingFederationPeerQuery(string? ClientId) : IRequest<PingFederationPeerResult>;

public record PingFederationPeerResult(string PeerName);

public class PingFederationPeerQueryHandler(IPeerAuthorizationService peerAuthorization)
    : IRequestHandler<PingFederationPeerQuery, PingFederationPeerResult>
{
    public async Task<PingFederationPeerResult> Handle(
        PingFederationPeerQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);
        return new PingFederationPeerResult(peer.Name);
    }
}
