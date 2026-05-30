using K7.Server.Application.Features.Stats.Queries.GetWatchStats;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetAdminWatchStats : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/stats", async (
            [FromServices] ISender sender,
            [FromQuery] MediaType? mediaType = null,
            [FromQuery] string period = "month",
            [FromQuery] Guid? userId = null,
            CancellationToken cancellationToken = default) =>
        {
            return await sender.Send(new GetWatchStatsQuery(mediaType, period, userId, GlobalStats: true), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
