using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Authentication;

public class GetServerInfo : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/server-info", async (
            [FromServices] IServerSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var guestEnabled = await settingsService.GetAsync(ServerSettingKeys.GuestEnabled, cancellationToken) == true;

            return Results.Ok(new { guestEnabled });
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
