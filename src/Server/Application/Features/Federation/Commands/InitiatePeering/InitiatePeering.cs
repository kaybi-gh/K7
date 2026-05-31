using System.Security.Cryptography;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
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
    IServerSettingsService serverSettingsService)
    : IRequestHandler<InitiatePeeringCommand, Guid>
{
    public async Task<Guid> Handle(InitiatePeeringCommand request, CancellationToken cancellationToken)
    {
        var flags = await GetFeatureFlagsAsync(cancellationToken);
        if (!flags.FederationInvitationsEnabled)
            throw new InvalidOperationException("Federation invitations are disabled on this server.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var peer = new PeerServer
        {
            Id = Guid.NewGuid(),
            Name = new Uri(request.RemoteUrl).Host,
            BaseUrl = request.RemoteUrl.TrimEnd('/'),
            Status = PeerStatus.Pending,
            OutboundClientId = null,
            OutboundClientSecret = null
        };

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

    private async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}
