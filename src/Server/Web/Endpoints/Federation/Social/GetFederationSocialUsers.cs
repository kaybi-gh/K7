using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialUsers : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users", async (
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

            var users = await context.Users
                .AsNoTracking()
                .Where(u => u.PeerServerId == null && u.IsActive && u.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var result = new List<FederatedUserRef>();
            foreach (var user in users)
            {
                var privacy = await privacyService.GetPrivacyAsync(user.Id, cancellationToken);
                var discoverableTypes = await GetDiscoverableContentTypesAsync(
                    context,
                    visibilityEvaluator,
                    viewer!.OriginUserId,
                    peer!.Id,
                    user.Id,
                    privacy,
                    cancellationToken);

                if (discoverableTypes.Count == 0)
                    continue;

                result.Add(new FederatedUserRef
                {
                    OriginUserId = user.Id,
                    DisplayName = user.DisplayName,
                    DiscoverableContentTypes = discoverableTypes
                });
            }

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static async Task<IReadOnlyList<FederationContentType>> GetDiscoverableContentTypesAsync(
        IApplicationDbContext context,
        IContentVisibilityEvaluator visibilityEvaluator,
        Guid viewerOriginUserId,
        Guid peerServerId,
        Guid ownerUserId,
        FederationPrivacySettingsDto privacy,
        CancellationToken cancellationToken)
    {
        var types = new List<FederationContentType>();

        foreach (var contentType in Enum.GetValues<FederationContentType>())
        {
            if (await IsDiscoverableContentTypeAsync(
                    context,
                    visibilityEvaluator,
                    viewerOriginUserId,
                    peerServerId,
                    ownerUserId,
                    contentType,
                    GetShareScope(privacy, contentType),
                    cancellationToken))
                types.Add(contentType);
        }

        return types;
    }

    private static async Task<bool> IsDiscoverableContentTypeAsync(
        IApplicationDbContext context,
        IContentVisibilityEvaluator visibilityEvaluator,
        Guid viewerOriginUserId,
        Guid peerServerId,
        Guid ownerUserId,
        FederationContentType contentType,
        VisibilityScope shareScope,
        CancellationToken cancellationToken)
    {
        if (shareScope == VisibilityScope.Nobody)
            return false;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(contentType, outbound: false, peerServerId, cancellationToken))
            return false;

        if (!await visibilityEvaluator.CanViewFederatedAsync(
                viewerOriginUserId, peerServerId, ownerUserId, contentType, shareScope, cancellationToken: cancellationToken))
            return false;

        if (contentType == FederationContentType.PlaybackHistory)
            return true;

        return contentType switch
        {
            FederationContentType.Reviews => await context.MediaReviews.AnyAsync(r => r.UserId == ownerUserId, cancellationToken),
            FederationContentType.Collections => await context.Collections.AnyAsync(c => c.UserId == ownerUserId && c.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.Playlists => await context.Playlists.AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            FederationContentType.SmartPlaylists => await context.Playlists.OfType<Domain.Entities.Playlists.SmartPlaylist>().AnyAsync(p => p.UserId == ownerUserId && p.VisibilityScope != VisibilityScope.Nobody, cancellationToken),
            _ => false
        };
    }

    private static VisibilityScope GetShareScope(FederationPrivacySettingsDto privacy, FederationContentType contentType) =>
        contentType switch
        {
            FederationContentType.Reviews => privacy.Share.Reviews,
            FederationContentType.Collections => privacy.Share.Collections,
            FederationContentType.Playlists => privacy.Share.Playlists,
            FederationContentType.SmartPlaylists => privacy.Share.SmartPlaylists,
            FederationContentType.PlaybackHistory => privacy.Share.PlaybackHistory,
            _ => VisibilityScope.Nobody
        };
}
