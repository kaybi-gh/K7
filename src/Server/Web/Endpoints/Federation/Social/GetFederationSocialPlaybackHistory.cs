using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialPlaybackHistory : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/playback-history", async (
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
                FederationContentType.PlaybackHistory, outbound: false, peer!.Id, cancellationToken))
                return Results.Ok(Array.Empty<FederatedUserPlaybackEntryDto>());

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
                FederationContentType.PlaybackHistory,
                privacy.Share.PlaybackHistory,
                cancellationToken: cancellationToken))
                return Results.Ok(Array.Empty<FederatedUserPlaybackEntryDto>());

            var sessions = await context.MediaPlaybackSessions
                .AsNoTracking()
                .Include(s => s.Media)
                .Where(s => s.UserId == owner.Id)
                .OrderByDescending(s => s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            var dtos = sessions.Select(s => new FederatedUserPlaybackEntryDto
            {
                OriginUserId = owner.Id,
                MediaId = s.MediaId,
                MediaTitle = s.Media?.Title ?? "?",
                EndedAt = s.StoppedAt ?? s.LastUpdateAt ?? s.StartedAt
            }).ToList();

            return Results.Ok(dtos);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
