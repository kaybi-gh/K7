using System.Security.Cryptography;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Settings;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Federation.Commands.InitiatePeering;

[Authorize(Roles = Roles.Administrator)]
public record InitiatePeeringCommand : IRequest<Guid>
{
    public required string RemoteUrl { get; init; }
    public required string LocalServerName { get; init; }
    public required string LocalServerUrl { get; init; }
}

public class InitiatePeeringCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IServerSettingsService serverSettingsService,
    IPeerUrlGuard peerUrlGuard)
    : IRequestHandler<InitiatePeeringCommand, Guid>
{
    public async Task<Guid> Handle(InitiatePeeringCommand request, CancellationToken cancellationToken)
    {
        var flags = await serverSettingsService.GetFeatureFlagsAsync(cancellationToken);
        if (!flags.FederationInvitationsEnabled)
            throw new InvalidOperationException("Federation invitations are disabled on this server.");

        peerUrlGuard.EnsureAllowedOutgoingUrl(request.RemoteUrl);
        peerUrlGuard.EnsureAllowedOutgoingUrl(request.LocalServerUrl);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var peer = PeerServer.CreatePending(
            new Uri(request.RemoteUrl).Host,
            request.RemoteUrl.TrimEnd('/'),
            token);

        context.PeerServers.Add(peer);

        await peerClient.SendPeerRequestAsync(
            request.RemoteUrl,
            request.LocalServerName,
            request.LocalServerUrl,
            token,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return peer.Id;
    }
}
