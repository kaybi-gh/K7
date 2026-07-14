using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetFederationSocialUsers;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation.Social;

public class GetFederationSocialUsers : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/social/users", async (
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var viewerAssertion = httpContext.Request.Headers[IPeerAuthorizationService.ViewerAssertionHeader].FirstOrDefault();
            var result = await sender.Send(new GetFederationSocialUsersQuery(clientId, viewerAssertion), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
