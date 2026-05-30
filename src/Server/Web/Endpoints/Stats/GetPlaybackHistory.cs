using K7.Server.Application.Features.Stats.Queries.GetPlaybackHistory;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Stats;

public class GetPlaybackHistory : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/stats/history", async (
            [FromServices] ISender sender,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] MediaType? mediaType = null,
            CancellationToken cancellationToken = default) =>
        {
            return await sender.Send(new GetPlaybackHistoryQuery
            {
                Page = page,
                PageSize = pageSize,
                MediaType = mediaType
            }, cancellationToken);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
