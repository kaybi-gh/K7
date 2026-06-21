using K7.Server.Application.Features.Medias.Commands.SetMediaWatchState;
using K7.Server.Domain.Constants;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class SetMediaWatchState : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/medias/{mediaId:guid}/watch-state", async (
            [FromServices] ISender sender,
            [FromRoute] Guid mediaId,
            [FromBody] SetMediaWatchStateRequest request,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new SetMediaWatchStateCommand(mediaId, request.Watched, request.Scope),
                cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record SetMediaWatchStateRequest(bool Watched, WatchStateScope Scope = WatchStateScope.Item);
