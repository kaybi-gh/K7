using K7.Server.Application.Features.Notifications.Commands.UpdateNotificationRule;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class UpdateNotificationRule : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/notifications/rules/{id}", async ([FromServices] ISender sender, Guid id, UpdateNotificationRuleRequest request, CancellationToken cancellationToken) =>
        {
            var command = new UpdateNotificationRuleCommand
            {
                Id = id,
                Name = request.Name,
                ProviderType = request.ProviderType,
                EventTypeName = request.EventTypeName,
                ProviderConfig = request.ProviderConfig,
                PayloadTemplate = request.PayloadTemplate,
                Conditions = request.Conditions,
                ConditionsLogic = request.ConditionsLogic,
                IsEnabled = request.IsEnabled
            };

            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
