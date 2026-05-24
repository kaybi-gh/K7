using K7.Server.Application.Features.Notifications.Commands.CreateNotificationRule;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class CreateNotificationRule : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/notifications/rules", async ([FromServices] ISender sender, CreateNotificationRuleRequest request, CancellationToken cancellationToken) =>
        {
            var command = new CreateNotificationRuleCommand
            {
                Name = request.Name,
                ProviderType = request.ProviderType,
                PayloadFormat = request.PayloadFormat,
                EventTypeNames = request.EventTypeNames,
                ProviderConfig = request.ProviderConfig,
                TitleTemplate = request.TitleTemplate,
                BodyTemplate = request.BodyTemplate,
                RawJsonTemplate = request.RawJsonTemplate,
                RuleFilter = request.RuleFilter,
                IsEnabled = request.IsEnabled
            };

            var id = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/notifications/rules/{id}", id);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
