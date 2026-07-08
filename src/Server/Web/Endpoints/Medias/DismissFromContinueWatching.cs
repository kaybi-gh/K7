using K7.Server.Application.Features.Medias.Commands.DismissFromContinueWatching;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class DismissFromContinueWatching : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/{mediaId:guid}/dismiss-continue-watching", async (
            [FromServices] ISender sender,
            [FromRoute] Guid mediaId,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DismissFromContinueWatchingCommand(mediaId), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
