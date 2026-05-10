using K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Home;

public class GetHomeFeed : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/home/feed", async (
            [FromServices] ISender sender,
            [AsParameters] GetHomeFeedItemsQuery query) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result.ToDto(x => x));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
