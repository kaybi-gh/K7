using K7.Server.Application.Features.LastVideoTrackSelection.Commands.UpsertLastVideoTrackSelection;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpsertLastVideoTrackSelection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/last-video-track-selection/{mediaId:guid}", async (
            Guid mediaId,
            [FromBody] LastVideoTrackSelectionDto selection,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new UpsertLastVideoTrackSelectionCommand { MediaId = mediaId, Selection = selection },
                cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
