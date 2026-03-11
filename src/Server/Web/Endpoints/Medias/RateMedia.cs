using K7.Server.Application.Features.Medias.Commands.RateMedia;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class RateMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/medias/{mediaId:guid}/rating", async (
            [FromServices] ISender sender,
            [FromRoute] Guid mediaId,
            [FromBody] RateMediaRequest request) =>
        {
            await sender.Send(new RateMediaCommand(mediaId, request.Value));
            return Results.NoContent();
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record RateMediaRequest(int Value);
