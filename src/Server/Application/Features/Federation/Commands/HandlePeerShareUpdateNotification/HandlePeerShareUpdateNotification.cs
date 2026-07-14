using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.HandlePeerShareUpdateNotification;

public record HandlePeerShareUpdateNotificationCommand(string? ClientId, ShareUpdateNotifyRequest Request) : IRequest;

public class HandlePeerShareUpdateNotificationCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    ISender sender)
    : IRequestHandler<HandlePeerShareUpdateNotificationCommand>
{
    public async Task Handle(HandlePeerShareUpdateNotificationCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(command.ClientId, cancellationToken);
        await sender.Send(new SyncPeerMetadataCommand(peer.Id), cancellationToken);
    }
}
