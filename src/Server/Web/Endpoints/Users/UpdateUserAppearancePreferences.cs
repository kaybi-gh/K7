using K7.Server.Application.Features.Appearance.Commands.UpdateAppearancePreferences;
using K7.Server.Domain.Constants;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserAppearancePreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/preferences/appearance", async (
            [FromBody] AppearancePreferencesDto preferences,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateAppearancePreferencesCommand { Preferences = preferences }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
