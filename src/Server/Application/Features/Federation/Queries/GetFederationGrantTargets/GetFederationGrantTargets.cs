using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationGrantTargets;

[Authorize]
public record GetFederationGrantTargetsQuery : IRequest<IReadOnlyList<FederationGrantTargetDto>>;

public class GetFederationGrantTargetsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetFederationGrantTargetsQuery, IReadOnlyList<FederationGrantTargetDto>>
{
    public async Task<IReadOnlyList<FederationGrantTargetDto>> Handle(
        GetFederationGrantTargetsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return [];

        var targets = new List<FederationGrantTargetDto>();

        var localUsers = await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.PeerServerId == null && u.DeletedAt == null && u.Id != userId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        foreach (var user in localUsers)
        {
            targets.Add(new FederationGrantTargetDto
            {
                Kind = FederationGrantTargetKind.LocalUser,
                Label = user.DisplayName ?? user.Id.ToString(),
                TargetUserId = user.Id
            });
        }

        var peers = await context.PeerServers
            .AsNoTracking()
            .Where(p => p.Status == PeerStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        foreach (var peer in peers)
        {
            targets.Add(new FederationGrantTargetDto
            {
                Kind = FederationGrantTargetKind.PeerServer,
                Label = peer.Name,
                TargetPeerServerId = peer.Id
            });
        }

        var virtualUsers = await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.PeerServerId != null && u.OriginUserId != null)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        foreach (var virtualUser in virtualUsers)
        {
            var peer = peers.FirstOrDefault(p => p.Id == virtualUser.PeerServerId);
            var peerName = peer?.Name ?? virtualUser.PeerServerId?.ToString() ?? "peer";
            var displayName = virtualUser.DisplayName ?? virtualUser.OriginUserId?.ToString() ?? "user";
            targets.Add(new FederationGrantTargetDto
            {
                Kind = FederationGrantTargetKind.FederatedUser,
                Label = $"{displayName} ({peerName})",
                TargetPeerServerId = virtualUser.PeerServerId,
                TargetOriginUserId = virtualUser.OriginUserId
            });
        }

        return targets;
    }
}
