using K7.Server.Application.Features.TrackSelectionPreferences.Commands.UpdateDefaultTrackSelectionPreferences;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateServerTrackSelectionPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/server/preferences/track-selection", async (
            [FromBody] TrackSelectionPreferencesDto settings,
            [FromQuery] Guid? libraryId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateDefaultTrackSelectionPreferencesCommand { Settings = settings, LibraryId = libraryId }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
