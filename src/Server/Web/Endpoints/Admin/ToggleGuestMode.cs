using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class ToggleGuestMode : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/guest", async ([FromBody] ToggleGuestRequest request, [FromServices] IServerSettingsService settingsService, CancellationToken cancellationToken) =>
        {
            await settingsService.SetAsync(ServerSettingKeys.GuestEnabled, request.Enabled, cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record ToggleGuestRequest(bool Enabled);
