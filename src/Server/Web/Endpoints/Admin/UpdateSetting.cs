using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateSetting : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/{key}", async (
            [FromRoute] string key,
            [FromBody] JsonElement body,
            [FromServices] IServerSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (!ServerSettingKeys.AdminEditableKeys.TryGetValue(key, out var settingKey))
            {
                return Results.NotFound();
            }

            object? value;
            try
            {
                value = body.Deserialize(settingKey.ValueType);
            }
            catch (JsonException)
            {
                return Results.BadRequest($"Invalid value for setting '{key}' (expected {settingKey.ValueType.Name}).");
            }

            if (value is null)
            {
                return Results.BadRequest("Value cannot be null.");
            }

            await settingsService.SetAsync(settingKey, value, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
