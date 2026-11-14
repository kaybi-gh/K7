using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateDefaultTheme : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/default-theme", async (
            [FromBody] UpdateDefaultThemeRequest request,
            [FromServices] IServerSettingsService serverSettings,
            CancellationToken cancellationToken) =>
        {
            if (!BuiltInThemes.Contains(request.Theme))
            {
                var serverThemes = ServerThemes.Parse(await serverSettings.GetAsync(ServerSettingKeys.ServerThemes, cancellationToken));
                if (!serverThemes.Any(t => string.Equals(t.Id, request.Theme, StringComparison.Ordinal)))
                    return Results.BadRequest("Unsupported theme.");
            }

            await serverSettings.SetAsync(ServerSettingKeys.DefaultTheme, request.Theme, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateDefaultThemeRequest(string Theme);
