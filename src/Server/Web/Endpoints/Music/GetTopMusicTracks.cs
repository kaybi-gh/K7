using K7.Server.Application.Features.Music.Queries.GetTopMusicTracks;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Music;

public class GetTopMusicTracks : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/music/top-tracks", async (
            [FromServices] ISender sender,
            [AsParameters] GetTopMusicTracksQuery query,
            CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(query, cancellationToken)))
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
