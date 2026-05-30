using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.RejectPeerRequest;

[Authorize(Roles = Roles.Administrator)]
public record RejectPeerRequestCommand(Guid RequestId) : IRequest;

public class RejectPeerRequestCommandHandler(IApplicationDbContext context)
    : IRequestHandler<RejectPeerRequestCommand>
{
    public async Task Handle(RejectPeerRequestCommand request, CancellationToken cancellationToken)
    {
        var peerRequest = await context.PeerRequests
            .FindAsync([request.RequestId], cancellationToken);

        Guard.Against.NotFound(request.RequestId, peerRequest);

        if (peerRequest.Status != PeerRequestStatus.Pending)
            throw new InvalidOperationException("This peer request has already been processed.");

        peerRequest.Status = PeerRequestStatus.Rejected;
        peerRequest.RespondedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}
