using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class DeleteUserLanguage : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/users/me/language", async (
            [FromServices] IUser currentUser,
            [FromServices] IUserSettingsService userSettings,
            CancellationToken cancellationToken) =>
        {
            Guard.Against.Null(currentUser.Id);

            await userSettings.RemoveAsync(currentUser.Id.Value, UserSettingKeys.Language, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
