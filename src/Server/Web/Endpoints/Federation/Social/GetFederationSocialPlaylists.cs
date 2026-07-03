using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialPlaylists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/playlists", async (
            Guid originUserId,
            [FromServices] IApplicationDbContext context,
            [FromServices] IFederationViewerAssertionService assertionService,
            [FromServices] IUserFederationPrivacyService privacyService,
            [FromServices] IContentVisibilityEvaluator visibilityEvaluator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (peer, viewer, error) = await FederationSocialEndpointHelper.ResolvePeerAndViewerAsync(
                httpContext, context, assertionService, cancellationToken);
            if (error is not null)
                return error;

            if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.Playlists, outbound: false, peer!.Id, cancellationToken))
                return Results.Ok(Array.Empty<FederatedPlaylistDto>());

            var owner = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == originUserId && u.PeerServerId == null, cancellationToken);

            if (owner is null)
                return Results.NotFound();

            var privacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
            if (!await visibilityEvaluator.CanViewFederatedAsync(
                viewer!.OriginUserId,
                peer!.Id,
                owner.Id,
                FederationContentType.Playlists,
                privacy.Share.Playlists,
                cancellationToken: cancellationToken))
                return Results.Ok(Array.Empty<FederatedPlaylistDto>());

            var playlists = await context.Playlists
                .AsNoTracking()
                .Include(p => p.Items)
                    .ThenInclude(i => i.Media)
                        .ThenInclude(m => m!.ExternalIds)
                .Where(p => p.UserId == owner.Id && p.VisibilityScope != VisibilityScope.Nobody)
                .ToListAsync(cancellationToken);

            var dtos = new List<FederatedPlaylistDto>();
            foreach (var playlist in playlists.Where(p => p is not SmartPlaylist))
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
                        .OrderBy(i => i.Order)
                        .Select(i => new FederatedPlaylistItemDto
                        {
                            Media = FederationSocialEndpointHelper.ToMediaRef(i.Media),
                            Order = i.Order
                        })
                        .ToList()
                });
            }

            return Results.Ok(dtos);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
