using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.RevokePeer;

[Authorize(Roles = Roles.Administrator)]
public record RevokePeerCommand(Guid PeerId) : IRequest;

public class RevokePeerCommandHandler(
    IApplicationDbContext context,
    IPeerApplicationManager peerAppManager)
    : IRequestHandler<RevokePeerCommand>
{
    public async Task Handle(RevokePeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .Include(p => p.ShareAgreements)
            .Include(p => p.RemoteIndexedFiles)
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        if (peer.InboundApplicationId is not null)
        {
            await peerAppManager.DeletePeerApplicationAsync(peer.InboundApplicationId, cancellationToken);
        }

        var virtualUsers = await context.Users
            .Where(u => u.PeerServerId == peer.Id)
            .ToListAsync(cancellationToken);

        context.Users.RemoveRange(virtualUsers);

        var remoteLibraries = await context.Libraries
            .Where(l => l.PeerServerId == peer.Id)
            .ToListAsync(cancellationToken);

        context.Libraries.RemoveRange(remoteLibraries);
        context.PeerServers.Remove(peer);

        await context.SaveChangesAsync(cancellationToken);
    }
}
