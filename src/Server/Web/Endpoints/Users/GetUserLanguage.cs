using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetUserLanguage : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/language", async (
            [FromServices] IUser currentUser,
            [FromServices] IUserSettingsService userSettings,
            CancellationToken cancellationToken) =>
        {
            Guard.Against.Null(currentUser.Id);

            var language = await userSettings.GetAsync(currentUser.Id.Value, UserSettingKeys.Language, cancellationToken);
            return Results.Ok(new { Language = language });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
