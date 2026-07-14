using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialPlaybackHistory;

public record GetFederationSocialPlaybackHistoryQuery(string? ClientId, string? ViewerAssertion, Guid OriginUserId)
    : IRequest<IReadOnlyList<FederatedUserPlaybackEntryDto>>;

public class GetFederationSocialPlaybackHistoryQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : IRequestHandler<GetFederationSocialPlaybackHistoryQuery, IReadOnlyList<FederatedUserPlaybackEntryDto>>
{
    public async Task<IReadOnlyList<FederatedUserPlaybackEntryDto>> Handle(
        GetFederationSocialPlaybackHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.PlaybackHistory, outbound: false, peer.Id, cancellationToken))
            return [];

        var owner = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.OriginUserId && u.PeerServerId == null, cancellationToken);

        if (owner is null)
            throw new NotFoundException(request.OriginUserId.ToString(), "User");

        var privacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
        if (!await visibilityEvaluator.CanViewFederatedAsync(
                viewer.OriginUserId,
                peer.Id,
                owner.Id,
                FederationContentType.PlaybackHistory,
                privacy.Share.PlaybackHistory,
                cancellationToken: cancellationToken))
            return [];

        var sessions = await context.MediaPlaybackSessions
            .AsNoTracking()
            .Include(s => s.Media)
            .Where(s => s.UserId == owner.Id)
            .OrderByDescending(s => s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new FederatedUserPlaybackEntryDto
        {
            OriginUserId = owner.Id,
            MediaId = s.MediaId,
            MediaTitle = s.Media?.Title ?? "?",
            EndedAt = s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt
        }).ToList();
    }
}
