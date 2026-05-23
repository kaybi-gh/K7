using K7.Server.Application.Features.Notifications.Queries.GetNotificationRules;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class GetNotificationRules : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/notifications/rules", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            var rules = await sender.Send(new GetNotificationRulesQuery(), cancellationToken);
            return rules.Select(ToDto);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static NotificationRuleDto ToDto(NotificationRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        IsEnabled = rule.IsEnabled,
        ProviderType = rule.ProviderType.ToString(),
        EventTypeName = rule.EventTypeName,
        ProviderConfig = rule.ProviderConfig,
        PayloadTemplate = rule.PayloadTemplate,
        Conditions = rule.Conditions,
        ConditionsLogic = rule.ConditionsLogic,
        Created = rule.Created,
        LastModified = rule.LastModified
    };
}
