using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Services;

public class OutboundNotificationDispatcher(
    IApplicationDbContext context,
    IServiceProvider serviceProvider,
    NotificationConditionEvaluator conditionEvaluator,
    NotificationPayloadRenderer payloadRenderer,
    NotificationEventEnricher enricher,
    ILogger<OutboundNotificationDispatcher> logger)
{
    public async Task DispatchAsync(
        string eventTypeName,
        IReadOnlyDictionary<string, object?> eventData,
        Domain.Common.BaseEvent? domainEvent,
        CancellationToken cancellationToken)
    {
        var rules = await context.NotificationRules
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        var matchingRules = rules.Where(r => r.EventTypeNames.Contains(eventTypeName)).ToList();
        if (matchingRules.Count == 0)
            return;

        var enrichedData = domainEvent is not null
            ? await enricher.EnrichAsync(domainEvent, eventData, cancellationToken)
            : enricher.EnrichWithGlobals(eventData);

        foreach (var rule in matchingRules)
        {
            try
            {
                await ProcessRuleAsync(rule, enrichedData, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process notification rule {RuleId} ({RuleName})", rule.Id, rule.Name);
            }
        }
    }

    private async Task ProcessRuleAsync(
        NotificationRule rule,
        IReadOnlyDictionary<string, object?> eventData,
        CancellationToken cancellationToken)
    {
        if (!NotificationScheduleEvaluator.IsWithinWindow(rule.ScheduleWindows, DateTimeOffset.Now))
        {
            logger.LogDebug("Notification rule {RuleId} outside schedule window, skipping", rule.Id);
            return;
        }

        if (rule.CooldownSeconds is > 0 && rule.LastSentAt is DateTimeOffset lastSent)
        {
            var elapsed = DateTimeOffset.UtcNow - lastSent;
            if (elapsed < TimeSpan.FromSeconds(rule.CooldownSeconds.Value))
            {
                logger.LogDebug("Notification rule {RuleId} in cooldown, skipping", rule.Id);
                return;
            }
        }

        if (!conditionEvaluator.Evaluate(rule.RuleFilter, eventData))
        {
            logger.LogDebug("Notification rule {RuleId} conditions not met, skipping", rule.Id);
            return;
        }

        var payload = BuildPayload(rule, eventData);
        var provider = serviceProvider.GetRequiredKeyedService<INotificationProvider>(rule.ProviderType);
        var success = await provider.SendAsync(rule.ProviderConfig, payload, cancellationToken);

        if (success)
        {
            logger.LogDebug("Notification sent for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);

            await context.NotificationRules
                .Where(r => r.Id == rule.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSentAt, DateTimeOffset.UtcNow), cancellationToken);
        }
        else
        {
            logger.LogError("Notification delivery failed for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);
        }
    }

    private string BuildPayload(NotificationRule rule, IReadOnlyDictionary<string, object?> eventData)
    {
        if (rule.PayloadFormat == NotificationPayloadFormat.RawJson)
            return payloadRenderer.Render(rule.RawJsonTemplate, eventData);

        var title = payloadRenderer.RenderPlain(rule.TitleTemplate, eventData);
        var body = payloadRenderer.RenderPlain(rule.BodyTemplate, eventData);
        return JsonSerializer.Serialize(new { title, body }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
