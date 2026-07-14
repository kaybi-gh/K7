using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationPlaybackHistory;

public record GetFederationPlaybackHistoryQuery(string? ClientId) : IRequest<IReadOnlyList<FederationPlaybackEntryDto>>;

public class GetFederationPlaybackHistoryQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationPlaybackHistoryQuery, IReadOnlyList<FederationPlaybackEntryDto>>
{
    public async Task<IReadOnlyList<FederationPlaybackEntryDto>> Handle(
        GetFederationPlaybackHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);

        var sharingEnabled = await context.PeerShareAgreements
            .AnyAsync(a => a.PeerServerId == peer.Id
                && a.Direction == ShareDirection.Outbound
                && a.IsEnabled
                && a.SharePlaybackHistory, cancellationToken);

        if (!sharingEnabled)
            return [];

        return await context.StreamSessions
            .Where(s => s.PeerServerId == peer.Id && s.EndedAt != null && s.IndexedFileId != null)
            .OrderByDescending(s => s.EndedAt)
            .Take(100)
            .Select(s => new FederationPlaybackEntryDto
            {
                FileId = s.IndexedFileId!.Value,
                UserDisplayName = s.User != null ? s.User.DisplayName ?? "Unknown" : "Unknown",
                Position = s.Position,
                EndedAt = s.EndedAt!.Value
            })
            .ToListAsync(cancellationToken);
    }
}
