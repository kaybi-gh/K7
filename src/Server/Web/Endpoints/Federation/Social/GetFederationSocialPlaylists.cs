using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetFederationSocialPlaylists;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialPlaylists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users/{originUserId:guid}/playlists", async (
            Guid originUserId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var viewerAssertion = httpContext.Request.Headers[IPeerAuthorizationService.ViewerAssertionHeader].FirstOrDefault();
            var result = await sender.Send(
                new GetFederationSocialPlaylistsQuery(clientId, viewerAssertion, originUserId),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
