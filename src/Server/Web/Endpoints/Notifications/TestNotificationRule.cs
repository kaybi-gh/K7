using K7.Server.Application.Features.Notifications.Commands.TestNotificationRule;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class TestNotificationRule : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/notifications/rules/{id}/test", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var success = await sender.Send(new TestNotificationRuleCommand(id), cancellationToken);
            return Results.Ok(new { Success = success });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
