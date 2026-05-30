using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.ConfirmPeering;

public record ConfirmPeeringCommand : IRequest
{
    public required string Token { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}

public class ConfirmPeeringCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ConfirmPeeringCommand>
{
    public async Task Handle(ConfirmPeeringCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Status == PeerStatus.Pending, cancellationToken);

        if (peer is null)
            throw new InvalidOperationException("No pending peer found for confirmation.");

        peer.OutboundClientId = request.ClientId;
        peer.OutboundClientSecret = request.ClientSecret;
        peer.Status = PeerStatus.Active;
        peer.LastSeen = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}
