using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Federation.Commands.ReceivePeerRequest;

public record ReceivePeerRequestCommand : IRequest
{
    public required string RequesterUrl { get; init; }
    public required string RequesterName { get; init; }
    public required string Token { get; init; }
}

public class ReceivePeerRequestCommandHandler(
    IApplicationDbContext context,
    IServerSettingsService serverSettingsService,
    IFederationNotifier federationNotifier,
    IPeerUrlGuard peerUrlGuard)
    : IRequestHandler<ReceivePeerRequestCommand>
{
    public async Task Handle(ReceivePeerRequestCommand request, CancellationToken cancellationToken)
    {
        var flags = await GetFeatureFlagsAsync(cancellationToken);
        if (!flags.FederationInvitationsEnabled)
            throw new InvalidOperationException("Federation invitations are disabled on this server.");

        peerUrlGuard.EnsureAllowedOutgoingUrl(request.RequesterUrl);

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

            await context.SaveChangesAsync(cancellationToken);

            await federationNotifier.NotifyPeerRequestReceivedAsync(new K7.Shared.Dtos.Entities.PeerRequestDto
            {
                Id = peerRequest.Id,
                RequesterUrl = peerRequest.RequesterUrl,
                RequesterName = peerRequest.RequesterName,
                Status = peerRequest.Status,
                Created = peerRequest.Created
            }, cancellationToken);

            return;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}
