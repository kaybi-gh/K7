using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.UpdatePeer;

[Authorize(Roles = Roles.Administrator)]
public record UpdatePeerCommand : IRequest
{
    public required Guid PeerId { get; init; }
    public string? BaseUrl { get; init; }
    public IReadOnlyList<Guid>? SharedLibraryIds { get; init; }
    public IReadOnlyList<Guid>? EnabledInboundAgreementIds { get; init; }
    public int? MaxConcurrentStreams { get; init; }
    public bool? AutoAddNewLibraries { get; init; }
}

public class UpdatePeerCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<UpdatePeerCommandHandler> logger)
    : IRequestHandler<UpdatePeerCommand>
{
    public async Task Handle(UpdatePeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            peer.BaseUrl = request.BaseUrl.TrimEnd('/');
        }

        if (request.AutoAddNewLibraries.HasValue)
        {
            peer.AutoAddNewLibraries = request.AutoAddNewLibraries.Value;
        }

        var sharedLibrariesChanged = false;

        if (request.SharedLibraryIds is not null)
        {
            await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var libraryId in request.SharedLibraryIds)
            {
                context.PeerShareAgreements.Add(new PeerShareAgreement
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    LibraryId = libraryId,
                    Direction = ShareDirection.Outbound,
                    MaxConcurrentStreams = request.MaxConcurrentStreams,
                    IsEnabled = true
                });
            }

            sharedLibrariesChanged = true;
        }

        if (request.EnabledInboundAgreementIds is not null)
        {
            var inboundAgreements = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Inbound)
                .ToListAsync(cancellationToken);

            foreach (var agreement in inboundAgreements)
            {
                agreement.IsEnabled = request.EnabledInboundAgreementIds.Contains(agreement.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Best-effort notification to the consumer peer about share changes
        if (sharedLibrariesChanged && peer.OutboundClientId is not null && peer.OutboundClientSecret is not null)
        {
            try
            {
                var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
                if (token is not null)
                {
                    await peerClient.NotifyShareUpdateAsync(peer.BaseUrl, token, request.SharedLibraryIds!, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify peer {PeerName} of share update (best-effort)", peer.Name);
            }
        }
    }
}
