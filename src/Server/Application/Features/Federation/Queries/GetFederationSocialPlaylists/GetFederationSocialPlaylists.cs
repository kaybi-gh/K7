using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialPlaylists;

public record GetFederationSocialPlaylistsQuery(string? ClientId, string? ViewerAssertion, Guid OriginUserId)
    : IRequest<IReadOnlyList<FederatedPlaylistDto>>;

public class GetFederationSocialPlaylistsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator,
    MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetFederationSocialPlaylistsQuery, IReadOnlyList<FederatedPlaylistDto>>
{
    public async Task<IReadOnlyList<FederatedPlaylistDto>> Handle(
        GetFederationSocialPlaylistsQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.Playlists, outbound: true, peer.Id, cancellationToken))
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
                FederationContentType.Playlists,
                privacy.Share.Playlists,
                cancellationToken: cancellationToken))
            return [];

        var playlists = await context.Playlists
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m!.ExternalIds)
            .Where(p => p.UserId == owner.Id && p.VisibilityScope != VisibilityScope.Nobody)
            .ToListAsync(cancellationToken);

        var mediaIds = playlists.SelectMany(p => p.Items).Select(i => i.MediaId).Distinct().ToList();
        HashSet<Guid> accessibleIds;
        if (mediaIds.Count == 0)
        {
            accessibleIds = [];
        }
        else
        {
            var accessible = await mediaAccessFilter.ApplyAllAsync(
                context.Medias.AsNoTracking(), owner.Id, cancellationToken);
            accessibleIds = (await accessible
                .Where(m => mediaIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        var dtos = new List<FederatedPlaylistDto>();
        foreach (var playlist in playlists.Where(p => p is not DynamicPlaylist))
        {
            if (!await visibilityEvaluator.CanViewFederatedAsync(
                    viewer.OriginUserId,
                    peer.Id,
                    owner.Id,
                    FederationContentType.Playlists,
                    playlist.VisibilityScope,
                    playlistId: playlist.Id,
                    cancellationToken: cancellationToken))
                continue;

            dtos.Add(new FederatedPlaylistDto
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                MediaType = playlist.MediaType,
                Items = playlist.Items
                    .Where(i => accessibleIds.Contains(i.MediaId))
                    .OrderBy(i => i.Order)
                    .Select(i => new FederatedPlaylistItemDto
                    {
                        Media = i.Media!.ToFederatedMediaRef(),
                        Order = i.Order
                    })
                    .ToList()
            });
        }

        return dtos;
    }
}
