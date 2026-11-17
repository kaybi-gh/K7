using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

/// <summary>
/// Stores the default media pages and players of the server: what accounts that did not set
/// theirs up are shown.
/// </summary>
public class UpdateDefaultSkins : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/skins", async (
            [FromBody] UpdateDefaultSkinsRequest request,
            [FromServices] IServerSettingsService serverSettings,
            CancellationToken cancellationToken) =>
        {
            if (!AppearanceSkins.IsValid(request.Skins))
                return Results.BadRequest("Skins are not valid.");

            await serverSettings.SetAsync(
                ServerSettingKeys.DefaultSkins,
                AppearanceSkins.Serialize(request.Skins),
                cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateDefaultSkinsRequest(AppearanceSettingsDto Skins);
