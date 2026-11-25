using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;

public record ReceiveProviderRevocationCommand(ProviderRevokeRequest Request) : IRequest;

public class ReceiveProviderRevocationCommandHandler(
    IApplicationDbContext context,
    IFederationNotifier federationNotifier)
    : IRequestHandler<ReceiveProviderRevocationCommand>
{
    public async Task Handle(ReceiveProviderRevocationCommand command, CancellationToken cancellationToken)
    {
        var normalized = FederationCallbackProof.Normalize(command.Request.ProviderUrl);
        // Only the requester side of an active peering accepts this callback.
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.BaseUrl == normalized
                && p.Status == PeerStatus.Active
                && p.OutboundClientId != null, cancellationToken);

        // Always verify so a missing peer and a bad signature take the same path.
        var verified = FederationCallbackProof.VerifyProviderRevoke(command.Request, peer?.FederationAssertionSecret);
        if (peer is null || !verified)
            throw new UnauthorizedAccessException();

        peer.Revoke();
        await context.SaveChangesAsync(cancellationToken);

        await federationNotifier.NotifyPeerStateChangedAsync(peer.Id, PeerStatus.Revoked, cancellationToken);
    }
}
