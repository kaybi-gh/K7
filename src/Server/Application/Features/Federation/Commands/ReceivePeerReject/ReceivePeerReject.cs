using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerReject;

public record ReceivePeerRejectCommand(PeerRejectRequest Request) : IRequest;

public class ReceivePeerRejectCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<ReceivePeerRejectCommand>
{
    public async Task Handle(ReceivePeerRejectCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.ResolvePeerByBaseUrlAsync(
            command.Request.ProviderUrl, PeerStatus.Pending, cancellationToken);

        if (peer is null)
            throw new NotFoundException(command.Request.ProviderUrl, nameof(Domain.Entities.Federation.PeerServer));

        peer.Reject();
        await context.SaveChangesAsync(cancellationToken);
    }
}
