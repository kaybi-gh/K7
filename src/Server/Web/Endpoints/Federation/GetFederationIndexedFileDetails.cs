using K7.Server.Application.Features.Federation.Queries.GetFederationIndexedFileDetails;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationIndexedFileDetails : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/indexed-files/{fileId:guid}", async (
            Guid fileId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var result = await sender.Send(new GetFederationIndexedFileDetailsQuery(clientId, fileId), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
