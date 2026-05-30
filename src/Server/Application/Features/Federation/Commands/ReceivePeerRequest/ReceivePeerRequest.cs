using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerRequest;

public record ReceivePeerRequestCommand : IRequest
{
    public required string RequesterUrl { get; init; }
    public required string RequesterName { get; init; }
    public required string Token { get; init; }
}

public class ReceivePeerRequestCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ReceivePeerRequestCommand>
{
    public async Task Handle(ReceivePeerRequestCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.PeerRequests
            .FirstOrDefaultAsync(r => r.RequesterUrl == request.RequesterUrl
                && r.Status == PeerRequestStatus.Pending, cancellationToken);

        if (existing is not null)
        {
            existing.Token = request.Token;
            existing.LastModified = DateTimeOffset.UtcNow;
        }
        else
        {
            var peerRequest = new PeerRequest
            {
                Id = Guid.NewGuid(),
                RequesterUrl = request.RequesterUrl,
                RequesterName = request.RequesterName,
                Token = request.Token,
                Status = PeerRequestStatus.Pending
            };

            context.PeerRequests.Add(peerRequest);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
