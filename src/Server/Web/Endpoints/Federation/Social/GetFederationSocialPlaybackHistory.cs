using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetFederationSocialPlaybackHistory;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialPlaybackHistory : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/playback-history", async (
            Guid originUserId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var viewerAssertion = httpContext.Request.Headers[IPeerAuthorizationService.ViewerAssertionHeader].FirstOrDefault();
            var result = await sender.Send(
                new GetFederationSocialPlaybackHistoryQuery(clientId, viewerAssertion, originUserId),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
