using K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetDefaultTrackSelectionPreferences;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetServerTrackSelectionPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/server/preferences/track-selection", async (
            [FromQuery] Guid? libraryId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDefaultTrackSelectionPreferencesQuery { LibraryId = libraryId }, cancellationToken);
            return result is not null ? Results.Ok(result) : Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
