using K7.Server.Application.Features.Federation.Queries.GetFederationMediaMetadata;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationMediaMetadata : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/media/{mediaId:guid}/metadata", async (
            Guid mediaId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var result = await sender.Send(new GetFederationMediaMetadataQuery(clientId, mediaId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
