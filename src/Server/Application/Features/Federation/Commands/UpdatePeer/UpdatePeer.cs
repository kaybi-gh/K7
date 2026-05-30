using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.UpdatePeer;

[Authorize(Roles = Roles.Administrator)]
public record UpdatePeerCommand : IRequest
{
    public required Guid PeerId { get; init; }
    public IReadOnlyList<Guid> SharedLibraryIds { get; init; } = [];
    public int? MaxConcurrentStreams { get; init; }
}

public class UpdatePeerCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdatePeerCommand>
{
    public async Task Handle(UpdatePeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .Include(p => p.ShareAgreements)
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        var outboundAgreements = peer.ShareAgreements
            .Where(a => a.Direction == ShareDirection.Outbound)
            .ToList();

        foreach (var agreement in outboundAgreements)
        {
            context.PeerShareAgreements.Remove(agreement);
        }

        foreach (var libraryId in request.SharedLibraryIds)
        {
            peer.ShareAgreements.Add(new PeerShareAgreement
            {
                Id = Guid.NewGuid(),
                PeerServerId = peer.Id,
                LibraryId = libraryId,
                Direction = ShareDirection.Outbound,
                MaxConcurrentStreams = request.MaxConcurrentStreams,
                IsEnabled = true
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
