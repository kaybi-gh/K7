using K7.Server.Application.Features.GeneralPreferences.Commands.DeleteUserGeneralPreferences;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class DeleteUserGeneralPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/users/me/preferences/general", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteUserGeneralPreferencesCommand(), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
