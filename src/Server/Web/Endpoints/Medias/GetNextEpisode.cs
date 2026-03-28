using K7.Server.Application.Features.Medias.Queries.GetNextEpisode;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetNextEpisode : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{serieId}/next-episode",
            async ([FromServices] ISender sender, Guid serieId, [FromQuery] Guid currentEpisodeId) =>
            {
                var result = await sender.Send(new GetNextEpisodeQuery(serieId, currentEpisodeId));
                return result is not null ? Results.Ok(result) : Results.NoContent();
            })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
