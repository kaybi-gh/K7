using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/settings", async (
            [FromServices] IServerSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var result = new Dictionary<string, object?>();

            foreach (var (name, key) in ServerSettingKeys.AdminEditableKeys)
            {
                result[name] = await settingsService.GetAsync(key, cancellationToken);
            }

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
