using K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteDefaultTrackSelectionPreferences;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class DeleteServerTrackSelectionPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/server/preferences/track-selection", async (
            [FromQuery] Guid? libraryId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteDefaultTrackSelectionPreferencesCommand { LibraryId = libraryId }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
