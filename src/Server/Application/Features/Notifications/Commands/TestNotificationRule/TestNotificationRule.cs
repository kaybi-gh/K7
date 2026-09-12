using System.Net.Http;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Commands.TestNotificationRule;

public record TestNotificationRuleResult(bool Success, string? Error = null);

[Authorize(Roles = Roles.Administrator)]
public record TestNotificationRuleCommand(Guid Id) : IRequest<TestNotificationRuleResult>;

public class TestNotificationRuleCommandHandler : IRequestHandler<TestNotificationRuleCommand, TestNotificationRuleResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationPayloadRenderer _payloadRenderer;
    private readonly IEnumerable<INotificationEventDescriptor> _descriptors;
    private readonly ILogger<TestNotificationRuleCommandHandler> _logger;

    public TestNotificationRuleCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        NotificationPayloadRenderer payloadRenderer,
        IEnumerable<INotificationEventDescriptor> descriptors,
        ILogger<TestNotificationRuleCommandHandler> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _payloadRenderer = payloadRenderer;
        _descriptors = descriptors;
        _logger = logger;
    }

    public async Task<TestNotificationRuleResult> Handle(TestNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var testData = BuildSampleData(entity.EventTypeNames);
        var title = _payloadRenderer.RenderPlain(entity.TitleTemplate, testData);
        var body = _payloadRenderer.RenderPlain(entity.BodyTemplate, testData);
        var provider = _serviceProvider.GetRequiredKeyedService<INotificationProvider>(entity.ProviderType);

        var payload = entity.PayloadFormat == NotificationPayloadFormat.RawJson
            ? _payloadRenderer.Render(entity.RawJsonTemplate, testData)
            : JsonSerializer.Serialize(new { title, body });

        try
        {
            var success = await provider.SendAsync(entity.ProviderConfig, payload, cancellationToken);

            if (success)
            {
                _logger.LogDebug("Test notification for rule {RuleId} ({RuleName}) succeeded",
                    entity.Id, entity.Name);
                return new TestNotificationRuleResult(true);
            }

            _logger.LogError("Test notification for rule {RuleId} ({RuleName}) failed",
                entity.Id, entity.Name);
            return new TestNotificationRuleResult(false, "Webhook returned a non-success status code");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Test notification for rule {RuleId} failed", entity.Id);
            return new TestNotificationRuleResult(false, ex.InnerException?.Message ?? ex.Message);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Test notification for rule {RuleId} timed out", entity.Id);
            return new TestNotificationRuleResult(false, "Request timed out");
        }
    }

    private Dictionary<string, object?> BuildSampleData(IReadOnlyList<string> eventTypeNames)
    {
        var descriptor = _descriptors.FirstOrDefault(d => eventTypeNames.Contains(d.EventTypeName))
            ?? _descriptors.FirstOrDefault();

        var parameters = NotificationParams.Globals.AsEnumerable();
        if (descriptor is not null)
            parameters = parameters.Concat(descriptor.Parameters);

        return parameters
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Name, p => (object?)p.SampleValue, StringComparer.OrdinalIgnoreCase);
    }
}
