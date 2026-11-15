using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateDefaultCardStyle : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/settings/cards", async (
            [FromBody] UpdateDefaultCardStyleRequest request,
            [FromServices] IServerSettingsService serverSettings,
            CancellationToken cancellationToken) =>
        {
            if (!MediaCardStyles.IsValid(request.Cards))
                return Results.BadRequest("Card style is not valid.");

            await serverSettings.SetAsync(
                ServerSettingKeys.DefaultCardStyle,
                MediaCardStyles.Serialize(request.Cards),
                cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateDefaultCardStyleRequest(MediaCardPreferencesDto Cards);
