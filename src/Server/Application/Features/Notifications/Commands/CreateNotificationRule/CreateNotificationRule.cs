using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Notifications.Commands.CreateNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record CreateNotificationRuleCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string PayloadFormat { get; init; }
    public required IReadOnlyList<string> EventTypeNames { get; init; }
    public required string ProviderConfig { get; init; }
    public string? TitleTemplate { get; init; }
    public string? BodyTemplate { get; init; }
    public string? RawJsonTemplate { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public class CreateNotificationRuleCommandHandler : IRequestHandler<CreateNotificationRuleCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateNotificationRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var providerType = Enum.Parse<Domain.Enums.NotificationProviderType>(request.ProviderType, ignoreCase: true);
        var payloadFormat = Enum.Parse<Domain.Enums.NotificationPayloadFormat>(request.PayloadFormat, ignoreCase: true);

        var entity = new NotificationRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            ProviderType = providerType,
            PayloadFormat = payloadFormat,
            EventTypeNames = request.EventTypeNames.ToList(),
            ProviderConfig = request.ProviderConfig,
            TitleTemplate = request.TitleTemplate,
            BodyTemplate = request.BodyTemplate,
            RawJsonTemplate = request.RawJsonTemplate,
            RuleFilter = request.RuleFilter?.ToRuleGroup()
        };

        entity.AddDomainEvent(new NotificationRuleCreatedEvent(entity));
        _context.NotificationRules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
