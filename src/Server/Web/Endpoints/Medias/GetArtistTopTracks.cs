using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetArtistTopTracks : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{id}/top-tracks", async ([FromServices] ISender sender, Guid id) =>
        {
            var result = await sender.Send(new GetArtistTopTracksQuery { ArtistId = id });
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
