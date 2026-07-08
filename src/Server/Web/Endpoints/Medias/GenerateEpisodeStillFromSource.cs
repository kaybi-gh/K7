using K7.Server.Application.Features.Medias.Commands.GenerateEpisodeStillFromSource;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GenerateEpisodeStillFromSource : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/{id}/pictures/generate-from-source", async (
            [FromServices] ISender sender,
            Guid id,
            CancellationToken cancellationToken) =>
        {
            var pictureId = await sender.Send(new GenerateEpisodeStillFromSourceCommand
            {
                MediaId = id
            }, cancellationToken);

            return pictureId is null ? Results.NoContent() : Results.Ok(pictureId);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
