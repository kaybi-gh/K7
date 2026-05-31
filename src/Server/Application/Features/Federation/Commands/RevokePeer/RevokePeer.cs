using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.RevokePeer;

[Authorize(Roles = Roles.Administrator)]
public record RevokePeerCommand(Guid PeerId) : IRequest;

public class RevokePeerCommandHandler(
    IApplicationDbContext context,
    IPeerApplicationManager peerAppManager,
    IPeerClient peerClient,
    IConfiguration configuration,
    ILogger<RevokePeerCommandHandler> logger)
    : IRequestHandler<RevokePeerCommand>
{
    public async Task Handle(RevokePeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .Include(p => p.ShareAgreements)
            .Include(p => p.RemoteIndexedFiles)
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        // Best-effort notification to the other peer
        if (peer.OutboundClientId is not null && peer.OutboundClientSecret is not null)
        {
            try
            {
                var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
                if (token is not null)
                {
                    await peerClient.NotifyRevocationAsync(peer.BaseUrl, token, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify peer {PeerName} of revocation (best-effort)", peer.Name);
            }
        }
        else
        {
            // Provider side: no outbound credentials, notify via unauthenticated endpoint
            var providerUrl = configuration.GetValue<string>("BaseUrl") ?? "";
            if (!string.IsNullOrEmpty(providerUrl))
            {
                try
                {
                    await peerClient.NotifyProviderRevocationAsync(peer.BaseUrl, providerUrl, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to notify requester {PeerName} of provider revocation (best-effort)", peer.Name);
                }
            }
        }

        if (peer.InboundApplicationId is not null)
        {
            await peerAppManager.DeletePeerApplicationAsync(peer.InboundApplicationId, cancellationToken);
        }

        // Dissociate virtual users from the peer (keep for audit trail)
        await context.Users
            .Where(u => u.PeerServerId == peer.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PeerServerId, (Guid?)null), cancellationToken);

        // Dissociate medias and persons from the peer (keep them as local orphans)
        await context.Medias
            .Where(m => m.PeerServerId == peer.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PeerServerId, (Guid?)null), cancellationToken);

        await context.Persons
            .Where(p => p.PeerServerId == peer.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PeerServerId, (Guid?)null), cancellationToken);

        await context.StreamSessions
            .Where(ss => ss.PeerServerId == peer.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(ss => ss.PeerServerId, (Guid?)null), cancellationToken);

        var remoteLibraries = await context.Libraries
            .Where(l => l.PeerServerId == peer.Id)
            .ToListAsync(cancellationToken);

        context.Libraries.RemoveRange(remoteLibraries);
        context.PeerServers.Remove(peer);

        await context.SaveChangesAsync(cancellationToken);
    }
}
