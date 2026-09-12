using K7.Server.Application.Features.Scrobbling.Queries.GetScrobblingAvailability;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Scrobbling;

public class GetScrobblingAvailability : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapGet("/api/scrobbling/availability", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetScrobblingAvailabilityQuery(), cancellationToken))
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName("GetScrobblingAvailability")
        .WithTags("Scrobbling");
    }
}
