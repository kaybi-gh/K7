using K7.Server.Application.Features.Music.Queries.GetMusicStats;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Music;

public class GetMusicStats : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/music/stats", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetMusicStatsQuery(), cancellationToken);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
