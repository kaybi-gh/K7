using K7.Server.Application.Features.Stats.Queries.GetWatchStats;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Stats;

public class GetWatchStats : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/stats", async (
            [FromServices] ISender sender,
            [FromQuery] MediaType? mediaType,
            [FromQuery] string period = "month",
            CancellationToken cancellationToken = default) =>
        {
            return await sender.Send(new GetWatchStatsQuery(mediaType, period), cancellationToken);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
