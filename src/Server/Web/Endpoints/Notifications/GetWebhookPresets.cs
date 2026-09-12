using K7.Server.Application.Features.Notifications.Queries.GetWebhookPresets;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class GetWebhookPresets : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/notifications/presets", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetWebhookPresetsQuery(), cancellationToken))
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
