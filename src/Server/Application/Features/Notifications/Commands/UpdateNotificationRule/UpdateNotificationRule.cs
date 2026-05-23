using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Notifications.Commands.UpdateNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record UpdateNotificationRuleCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string EventTypeName { get; init; }
    public required string ProviderConfig { get; init; }
    public string? PayloadTemplate { get; init; }
    public string? Conditions { get; init; }
    public string? ConditionsLogic { get; init; }
    public bool IsEnabled { get; init; }
}

public class UpdateNotificationRuleCommandHandler : IRequestHandler<UpdateNotificationRuleCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateNotificationRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var providerType = Enum.Parse<Domain.Enums.NotificationProviderType>(request.ProviderType, ignoreCase: true);

        entity.Name = request.Name;
        entity.IsEnabled = request.IsEnabled;
        entity.ProviderType = providerType;
        entity.EventTypeName = request.EventTypeName;
        entity.ProviderConfig = request.ProviderConfig;
        entity.PayloadTemplate = request.PayloadTemplate;
        entity.Conditions = request.Conditions;
        entity.ConditionsLogic = request.ConditionsLogic;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
