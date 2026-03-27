using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserLanguage : IEndpoint
{
    private static readonly HashSet<string> _supportedLanguages = ["en", "fr"];

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/language", async (
            [FromBody] UpdateUserLanguageRequest request,
            [FromServices] IUser currentUser,
            [FromServices] IUserSettingsService userSettings,
            CancellationToken cancellationToken) =>
        {
            Guard.Against.Null(currentUser.Id);

            if (!_supportedLanguages.Contains(request.Language))
                return Results.BadRequest("Unsupported language.");

            await userSettings.SetAsync(currentUser.Id.Value, UserSettingKeys.Language, request.Language, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateUserLanguageRequest(string Language);
