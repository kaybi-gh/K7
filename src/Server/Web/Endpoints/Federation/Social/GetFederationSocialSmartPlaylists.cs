using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialSmartPlaylists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/smart-playlists", async (
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
                FederationContentType.SmartPlaylists, outbound: false, peer!.Id, cancellationToken))
                return Results.Ok(Array.Empty<FederatedSmartPlaylistDto>());

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
                FederationContentType.SmartPlaylists,
                privacy.Share.SmartPlaylists,
                cancellationToken: cancellationToken))
                return Results.Ok(Array.Empty<FederatedSmartPlaylistDto>());

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

            return Results.Ok(dtos);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
