using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateDefaultLanguage : IEndpoint
{
    private static readonly HashSet<string> _supportedLanguages = ["en", "fr"];

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/default-language", async (
            [FromBody] UpdateDefaultLanguageRequest request,
            [FromServices] IServerSettingsService serverSettings,
            CancellationToken cancellationToken) =>
        {
            if (!_supportedLanguages.Contains(request.Language))
                return Results.BadRequest("Unsupported language.");

            await serverSettings.SetAsync(ServerSettingKeys.DefaultLanguage, request.Language, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateDefaultLanguageRequest(string Language);
