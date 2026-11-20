using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

/// <summary>
/// Stores the default media pages and players of the server: what accounts that did not set
/// theirs up are shown, on every device or on one type of device.
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
            var skins = new ServerSkinsDto { Default = request.Skins };
            if (request.DeviceSkins is not null)
            {
                skins.DeviceOverrides = request.DeviceSkins;
            }
            else
            {
                // A client from before the sets per device only sends the shared one: the sets
                // of the device types are not its to remove.
                var stored = AppearanceSkins.ParseSet(await serverSettings.GetAsync(ServerSettingKeys.DefaultSkins, cancellationToken));
                skins.DeviceOverrides = stored?.DeviceOverrides ?? [];
            }

            if (!AppearanceSkins.IsValid(skins))
                return Results.BadRequest("Skins are not valid.");

            await serverSettings.SetAsync(
                ServerSettingKeys.DefaultSkins,
                AppearanceSkins.Serialize(skins),
                cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

/// <param name="Skins">The set shared by every device.</param>
/// <param name="DeviceSkins">The types of devices with a set of their own. Null leaves the stored ones alone.</param>
public record UpdateDefaultSkinsRequest(
    AppearanceSettingsDto Skins,
    Dictionary<K7.Server.Domain.Enums.DeviceType, AppearanceSettingsDto>? DeviceSkins = null);
