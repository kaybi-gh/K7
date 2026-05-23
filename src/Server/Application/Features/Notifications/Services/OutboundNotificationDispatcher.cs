using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Services;

public class OutboundNotificationDispatcher
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationConditionEvaluator _conditionEvaluator;
    private readonly NotificationPayloadRenderer _payloadRenderer;
    private readonly ILogger<OutboundNotificationDispatcher> _logger;

    public OutboundNotificationDispatcher(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        NotificationConditionEvaluator conditionEvaluator,
        NotificationPayloadRenderer payloadRenderer,
        ILogger<OutboundNotificationDispatcher> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _conditionEvaluator = conditionEvaluator;
        _payloadRenderer = payloadRenderer;
        _logger = logger;
    }

    public async Task DispatchAsync(
        string eventTypeName,
        IReadOnlyDictionary<string, object?> eventData,
        CancellationToken cancellationToken)
    {
        var rules = await _context.NotificationRules
            .AsNoTracking()
            .Where(r => r.IsEnabled && r.EventTypeName == eventTypeName)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
            return;

        foreach (var rule in rules)
        {
            try
            {
                await ProcessRuleAsync(rule, eventData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification rule {RuleId} ({RuleName})", rule.Id, rule.Name);
            }
        }
    }

    private async Task ProcessRuleAsync(
        NotificationRule rule,
        IReadOnlyDictionary<string, object?> eventData,
        CancellationToken cancellationToken)
    {
        var conditions = DeserializeConditions(rule.Conditions);

        if (!_conditionEvaluator.Evaluate(conditions, rule.ConditionsLogic, eventData))
        {
            _logger.LogDebug("Notification rule {RuleId} conditions not met, skipping", rule.Id);
            return;
        }

        var payload = _payloadRenderer.Render(rule.PayloadTemplate, eventData);
        var provider = ResolveProvider(rule.ProviderType);

        var success = await provider.SendAsync(rule.ProviderConfig, payload, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Notification sent for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);
        }
        else
        {
            _logger.LogWarning("Notification delivery failed for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);
        }
    }

    private INotificationProvider ResolveProvider(NotificationProviderType providerType)
    {
        return _serviceProvider.GetRequiredKeyedService<INotificationProvider>(providerType);
    }

    private static IReadOnlyList<NotificationCondition> DeserializeConditions(string? conditionsJson)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson))
            return [];

        return JsonSerializer.Deserialize<List<NotificationCondition>>(conditionsJson, JsonOptions) ?? [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
