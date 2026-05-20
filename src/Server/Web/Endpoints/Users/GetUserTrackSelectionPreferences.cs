using K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetEffectiveTrackSelectionPreferences;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetUserTrackSelectionPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/preferences/track-selection", async (
            [FromQuery] Guid? libraryId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetEffectiveTrackSelectionPreferencesQuery { LibraryId = libraryId }, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
