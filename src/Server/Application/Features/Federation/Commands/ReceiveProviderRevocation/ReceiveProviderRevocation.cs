using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;

public record ReceiveProviderRevocationCommand(ProviderRevokeRequest Request) : IRequest;

public class ReceiveProviderRevocationCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IFederationNotifier federationNotifier)
    : IRequestHandler<ReceiveProviderRevocationCommand>
{
    public async Task Handle(ReceiveProviderRevocationCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.ResolvePeerByBaseUrlAsync(
            command.Request.ProviderUrl, PeerStatus.Active, cancellationToken);

        if (peer is null)
            throw new NotFoundException(command.Request.ProviderUrl, nameof(Domain.Entities.Federation.PeerServer));

        peer.Revoke();
        await context.SaveChangesAsync(cancellationToken);

        await federationNotifier.NotifyPeerStateChangedAsync(peer.Id, PeerStatus.Revoked, cancellationToken);
    }
}
