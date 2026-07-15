using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialSmartPlaylists;

public record GetFederationSocialSmartPlaylistsQuery(string? ClientId, string? ViewerAssertion, Guid OriginUserId)
    : IRequest<IReadOnlyList<FederatedSmartPlaylistDto>>;

public class GetFederationSocialSmartPlaylistsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : IRequestHandler<GetFederationSocialSmartPlaylistsQuery, IReadOnlyList<FederatedSmartPlaylistDto>>
{
    public async Task<IReadOnlyList<FederatedSmartPlaylistDto>> Handle(
        GetFederationSocialSmartPlaylistsQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.SmartPlaylists, outbound: false, peer.Id, cancellationToken))
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
                FederationContentType.SmartPlaylists,
                privacy.Share.SmartPlaylists,
                cancellationToken: cancellationToken))
            return [];

        var playlists = await context.Playlists
            .AsNoTracking()
            .OfType<SmartPlaylist>()
            .Where(p => p.UserId == owner.Id && p.VisibilityScope != VisibilityScope.Nobody)
            .ToListAsync(cancellationToken);

        var dtos = new List<FederatedSmartPlaylistDto>();
        foreach (var playlist in playlists)
        {
            if (!await visibilityEvaluator.CanViewFederatedAsync(
                    viewer.OriginUserId,
                    peer.Id,
                    owner.Id,
                    FederationContentType.SmartPlaylists,
                    playlist.VisibilityScope,
                    playlistId: playlist.Id,
                    cancellationToken: cancellationToken))
                continue;

            dtos.Add(new FederatedSmartPlaylistDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                RuleFilter = playlist.RuleFilter.ToRuleGroupDto(),
                Limit = playlist.Limit,
                OrderBy = playlist.OrderBy,
                OrderDescending = playlist.OrderDescending
            });
        }

        return dtos;
    }
}
