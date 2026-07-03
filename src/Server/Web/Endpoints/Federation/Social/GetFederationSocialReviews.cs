using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialReviews : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/reviews", async (
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
                FederationContentType.Reviews, outbound: false, peer!.Id, cancellationToken))
                return Results.Ok(Array.Empty<FederatedReviewDto>());

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
                FederationContentType.Reviews,
                privacy.Share.Reviews,
                cancellationToken: cancellationToken))
                return Results.Ok(Array.Empty<FederatedReviewDto>());

            var reviews = await context.MediaReviews
                .AsNoTracking()
                .Include(r => r.UserRating)
                .Include(r => r.Media)
                    .ThenInclude(m => m!.ExternalIds)
                .Where(r => r.UserId == owner.Id)
                .OrderByDescending(r => r.Created)
                .ToListAsync(cancellationToken);

            var dtos = reviews.Select(r => new FederatedReviewDto
            {
                Id = r.Id,
                Author = new FederatedUserRef
                {
                    OriginUserId = owner.Id,
                    DisplayName = owner.DisplayName
                },
                Media = FederationSocialEndpointHelper.ToMediaRef(r.Media),
                Text = r.Text,
                Emoji = r.Emoji,
                Rating = r.UserRating?.Value ?? 0,
                Created = r.Created
            }).ToList();

            return Results.Ok(dtos);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
