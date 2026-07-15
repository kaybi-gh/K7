using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Federation.Commands.TestPeer;

[Authorize(Roles = Roles.Administrator)]
public record TestPeerCommand(Guid PeerId) : IRequest<bool>;

public class TestPeerCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IPeerConnectivityService peerConnectivityService)
    : IRequestHandler<TestPeerCommand, bool>
{
    public async Task<bool> Handle(TestPeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        bool reachable;

        if (peer.OutboundClientId is null || peer.OutboundClientSecret is null)
        {
            reachable = await peerClient.IsReachableAsync(peer.BaseUrl, cancellationToken);
        }
        else
        {
            var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
            reachable = token is not null && await peerClient.PingAsync(peer.BaseUrl, token, cancellationToken);
        }

        await peerConnectivityService.RecordConnectivityAsync(peer.Id, reachable, cancellationToken);

        return reachable;
    }
}
