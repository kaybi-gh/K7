using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateServerThemes : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/themes", async (
            [FromBody] UpdateServerThemesRequest request,
            [FromServices] IServerSettingsService serverSettings,
            CancellationToken cancellationToken) =>
        {
            if (!ServerThemes.IsValid(request.Themes))
                return Results.BadRequest("Server themes are not valid.");

            await serverSettings.SetAsync(ServerSettingKeys.ServerThemes, ServerThemes.Serialize(request.Themes), cancellationToken);

            // A removed theme cannot stay the default one.
            var defaultTheme = await serverSettings.GetAsync(ServerSettingKeys.DefaultTheme, cancellationToken);
            if (ServerThemes.IsValidId(defaultTheme)
                && !request.Themes.Any(t => string.Equals(t.Id, defaultTheme, StringComparison.Ordinal)))
            {
                await serverSettings.SetAsync(ServerSettingKeys.DefaultTheme, BuiltInThemes.DefaultDarkId, cancellationToken);
            }

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateServerThemesRequest(List<CustomThemeDto> Themes);
