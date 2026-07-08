using K7.Server.Application.Features.UserSettings.Queries.UserSettingExists;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetUserSettingExists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/settings/exists", async (
            [FromQuery] string key,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var exists = await sender.Send(new UserSettingExistsQuery(key), cancellationToken);
            return Results.Ok(new { exists });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
