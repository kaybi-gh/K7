using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.AcceptPeerRequest;

[Authorize(Roles = Roles.Administrator)]
public record AcceptPeerRequestCommand : IRequest
{
    public required Guid RequestId { get; init; }
    public IReadOnlyList<Guid> SharedLibraryIds { get; init; } = [];
    public bool AutoShareNewLibraries { get; init; }
    public int? MaxConcurrentStreams { get; init; }
}

public class AcceptPeerRequestCommandHandler(
    IApplicationDbContext context,
    IPeerApplicationManager peerAppManager,
    IPeerClient peerClient)
    : IRequestHandler<AcceptPeerRequestCommand>
{
    public async Task Handle(AcceptPeerRequestCommand request, CancellationToken cancellationToken)
    {
        var peerRequest = await context.PeerRequests
            .FindAsync([request.RequestId], cancellationToken);

        Guard.Against.NotFound(request.RequestId, peerRequest);

        if (peerRequest.Status != PeerRequestStatus.Pending)
            throw new InvalidOperationException("This peer request has already been processed.");

        var clientId = $"peer-{Guid.NewGuid():N}";
        var clientSecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        await peerAppManager.CreatePeerApplicationAsync(clientId, clientSecret, $"Peer: {peerRequest.RequesterName}", cancellationToken);

        var peer = new PeerServer
        {
            Id = Guid.NewGuid(),
            Name = peerRequest.RequesterName,
            BaseUrl = peerRequest.RequesterUrl.TrimEnd('/'),
            Status = PeerStatus.Active,
            InboundApplicationId = clientId,
            AutoAddNewLibraries = request.AutoShareNewLibraries
        };

        peer.FederationAssertionSecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

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

        context.PeerServers.Add(peer);

        peerRequest.Status = PeerRequestStatus.Accepted;
        peerRequest.RespondedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await peerClient.SendPeerConfirmAsync(
            peerRequest.RequesterUrl,
            peerRequest.Token,
            clientId,
            clientSecret,
            peer.FederationAssertionSecret,
            cancellationToken);
    }
}
