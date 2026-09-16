using K7.Server.Application.Features.Music.Queries.GetMusicHitParade;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Music;

public class GetMusicHitParade : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/music/hit-parade", async (
            [FromServices] ISender sender,
            [AsParameters] GetMusicHitParadeQuery query,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(query, cancellationToken)))
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
