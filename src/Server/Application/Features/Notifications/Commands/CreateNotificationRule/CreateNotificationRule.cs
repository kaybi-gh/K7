using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Commands.CreateNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record CreateNotificationRuleCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string EventTypeName { get; init; }
    public required string ProviderConfig { get; init; }
    public string? PayloadTemplate { get; init; }
    public string? Conditions { get; init; }
    public string? ConditionsLogic { get; init; }
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

        var entity = new NotificationRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            ProviderType = providerType,
            EventTypeName = request.EventTypeName,
            ProviderConfig = request.ProviderConfig,
            PayloadTemplate = request.PayloadTemplate,
            Conditions = request.Conditions,
            ConditionsLogic = request.ConditionsLogic
        };

        entity.AddDomainEvent(new NotificationRuleCreatedEvent(entity));
        _context.NotificationRules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
