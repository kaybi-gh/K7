using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Commands.TestNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record TestNotificationRuleCommand(Guid Id) : IRequest<bool>;

public class TestNotificationRuleCommandHandler : IRequestHandler<TestNotificationRuleCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationPayloadRenderer _payloadRenderer;
    private readonly ILogger<TestNotificationRuleCommandHandler> _logger;

    public TestNotificationRuleCommandHandler(
        IApplicationDbContext context,
        IServiceProvider serviceProvider,
        NotificationPayloadRenderer payloadRenderer,
        ILogger<TestNotificationRuleCommandHandler> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _payloadRenderer = payloadRenderer;
        _logger = logger;
    }

    public async Task<bool> Handle(TestNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var testData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EventType"] = entity.EventTypeName,
            ["Media.Title"] = "Test Media Title",
            ["Media.MediaType"] = "Movie",
            ["Media.ReleaseYear"] = 2025,
            ["Library.Title"] = "Test Library",
            ["Library.MediaType"] = "Movies",
            ["Device.Name"] = "Test Device",
            ["Playlist.Title"] = "Test Playlist",
            ["Session.UserId"] = Guid.Empty.ToString()
        };

        var payload = _payloadRenderer.Render(entity.PayloadTemplate, testData);
        var provider = _serviceProvider.GetRequiredKeyedService<INotificationProvider>(entity.ProviderType);

        var success = await provider.SendAsync(entity.ProviderConfig, payload, cancellationToken);

        _logger.LogInformation("Test notification for rule {RuleId} ({RuleName}): {Result}",
            entity.Id, entity.Name, success ? "success" : "failed");

        return success;
    }
}
