using K7.Server.Application.Features.Federation.Queries.GetFederationHlsSegments;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationHlsSegments : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/stream-sessions/{sessionId:guid}/hls-segments", async (
            Guid sessionId,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var segments = await sender.Send(
                new GetFederationHlsSegmentsQuery(clientId, sessionId),
                cancellationToken);
            return Results.Ok(segments);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
