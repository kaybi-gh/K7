using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialCollections : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/collections", async (
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
                FederationContentType.Collections, outbound: false, peer!.Id, cancellationToken))
                return Results.Ok(Array.Empty<FederatedCollectionDto>());

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
                FederationContentType.Collections,
                privacy.Share.Collections,
                cancellationToken: cancellationToken))
                return Results.Ok(Array.Empty<FederatedCollectionDto>());

            var collections = await context.Collections
                .AsNoTracking()
                .Include(c => c.Items)
                    .ThenInclude(i => i.Media)
                        .ThenInclude(m => m!.ExternalIds)
                .Where(c => c.UserId == owner.Id && c.VisibilityScope != VisibilityScope.Nobody)
                .ToListAsync(cancellationToken);

            var dtos = new List<FederatedCollectionDto>();
            foreach (var collection in collections)
            {
                if (!await visibilityEvaluator.CanViewFederatedAsync(
                    viewer.OriginUserId,
                    peer.Id,
                    owner.Id,
                    FederationContentType.Collections,
                    collection.VisibilityScope,
                    collectionId: collection.Id,
                    cancellationToken: cancellationToken))
                    continue;

                dtos.Add(new FederatedCollectionDto
                {
                    Id = collection.Id,
                    Title = collection.Title,
                    Description = collection.Description,
                    MediaType = collection.MediaType,
                    Items = collection.Items
                        .OrderBy(i => i.Order)
                        .Select(i => new FederatedCollectionItemDto
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
