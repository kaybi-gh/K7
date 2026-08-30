using K7.Server.Application.Features.Federation.Queries.GetFederationStream;
using K7.Server.Domain.Constants;
using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationStream : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/federation/stream/{fileId:guid}", ["GET", "HEAD"], async (
            Guid fileId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var result = await sender.Send(new GetFederationStreamQuery(clientId, fileId), cancellationToken);
            return MediaStreamHttp.File(result.Path, result.MimeType);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
