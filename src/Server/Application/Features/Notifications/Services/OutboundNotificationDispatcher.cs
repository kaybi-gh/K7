using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
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
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        var matchingRules = rules.Where(r => r.EventTypeNames.Contains(eventTypeName)).ToList();

        if (matchingRules.Count == 0)
            return;

        foreach (var rule in matchingRules)
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
        if (!_conditionEvaluator.Evaluate(rule.RuleFilter, eventData))
        {
            _logger.LogDebug("Notification rule {RuleId} conditions not met, skipping", rule.Id);
            return;
        }

        var enrichedData = EnrichWithGlobals(eventData);
        var payload = BuildPayload(rule, enrichedData);
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

    private string BuildPayload(NotificationRule rule, IReadOnlyDictionary<string, object?> eventData)
    {
        if (rule.PayloadFormat == NotificationPayloadFormat.RawJson)
        {
            return _payloadRenderer.Render(rule.RawJsonTemplate, eventData);
        }

        var title = _payloadRenderer.Render(rule.TitleTemplate, eventData);
        var body = _payloadRenderer.Render(rule.BodyTemplate, eventData);
        return JsonSerializer.Serialize(new { title, body }, JsonOptions);
    }

    private INotificationProvider ResolveProvider(NotificationProviderType providerType)
    {
        return _serviceProvider.GetRequiredKeyedService<INotificationProvider>(providerType);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static IReadOnlyDictionary<string, object?> EnrichWithGlobals(IReadOnlyDictionary<string, object?> eventData)
    {
        var now = DateTime.UtcNow;
        var enriched = new Dictionary<string, object?>(eventData, StringComparer.OrdinalIgnoreCase)
        {
            ["Current.Year"] = now.Year,
            ["Current.Month"] = now.Month,
            ["Current.Day"] = now.Day,
            ["Current.Hour"] = now.Hour,
            ["Current.Minute"] = now.Minute,
            ["Current.Weekday"] = now.DayOfWeek.ToString(),
            ["Current.Datestamp"] = now.ToString("yyyy-MM-dd"),
            ["Current.Timestamp"] = now.ToString("o"),
        };
        return enriched;
    }
}
