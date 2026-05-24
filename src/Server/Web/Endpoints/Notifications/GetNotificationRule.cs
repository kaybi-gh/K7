using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Notifications.Queries.GetNotificationRule;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class GetNotificationRule : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/notifications/rules/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var rule = await sender.Send(new GetNotificationRuleQuery(id), cancellationToken);
            return new NotificationRuleDto
            {
                Id = rule.Id,
                Name = rule.Name,
                IsEnabled = rule.IsEnabled,
                ProviderType = rule.ProviderType.ToString(),
                PayloadFormat = rule.PayloadFormat.ToString(),
                EventTypeNames = rule.EventTypeNames,
                ProviderConfig = rule.ProviderConfig,
                TitleTemplate = rule.TitleTemplate,
                BodyTemplate = rule.BodyTemplate,
                RawJsonTemplate = rule.RawJsonTemplate,
                RuleFilter = rule.RuleFilter?.ToRuleGroupDto(),
                Created = rule.Created,
                LastModified = rule.LastModified
            };
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
