using K7.Server.Application.Features.GeneralPreferences.Commands.UpdateUserGeneralPreferences;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserGeneralPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/preferences/general", async (
            [FromBody] GeneralPreferencesDto settings,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserGeneralPreferencesCommand { Settings = settings }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
