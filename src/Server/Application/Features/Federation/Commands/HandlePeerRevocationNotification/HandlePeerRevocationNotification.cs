using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.HandlePeerRevocationNotification;

public record HandlePeerRevocationNotificationCommand(string? ClientId) : IRequest;

public class HandlePeerRevocationNotificationCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IFederationNotifier federationNotifier)
    : IRequestHandler<HandlePeerRevocationNotificationCommand>
{
    public async Task Handle(HandlePeerRevocationNotificationCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(command.ClientId, cancellationToken);

        peer.Revoke();
        await context.SaveChangesAsync(cancellationToken);

        await federationNotifier.NotifyPeerStateChangedAsync(peer.Id, PeerStatus.Revoked, cancellationToken);
    }
}
