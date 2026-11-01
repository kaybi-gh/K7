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

        // With currentEpisodeId: the episode after it (player next-episode offer).
        // Without: the episode the user should play on the series page (in progress, otherwise
        // first unwatched, otherwise the first episode). 204 when there is none.
        endpointRouteBuilder.MapGet("/api/medias/{serieId}/next-episode",
            async ([FromServices] ISender sender, Guid serieId, [FromQuery] Guid? currentEpisodeId, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetNextEpisodeQuery(serieId, currentEpisodeId), cancellationToken);
                return result is not null ? Results.Ok(result) : Results.NoContent();
            })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
