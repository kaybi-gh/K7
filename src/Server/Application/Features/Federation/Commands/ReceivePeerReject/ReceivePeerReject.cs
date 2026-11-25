using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerReject;

public record ReceivePeerRejectCommand(PeerRejectRequest Request) : IRequest;

public class ReceivePeerRejectCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ReceivePeerRejectCommand>
{
    public async Task Handle(ReceivePeerRejectCommand command, CancellationToken cancellationToken)
    {
        var token = command.Request.Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException();

        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Status == PeerStatus.Pending && p.PeeringToken == token, cancellationToken);

        if (peer is null)
            throw new UnauthorizedAccessException();

        peer.Reject();
        await context.SaveChangesAsync(cancellationToken);
    }
}
