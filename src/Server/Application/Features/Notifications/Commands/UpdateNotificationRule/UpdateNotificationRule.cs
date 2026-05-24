using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Notifications.Commands.UpdateNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record UpdateNotificationRuleCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string PayloadFormat { get; init; }
    public required IReadOnlyList<string> EventTypeNames { get; init; }
    public required string ProviderConfig { get; init; }
    public string? TitleTemplate { get; init; }
    public string? BodyTemplate { get; init; }
    public string? RawJsonTemplate { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
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
        var payloadFormat = Enum.Parse<Domain.Enums.NotificationPayloadFormat>(request.PayloadFormat, ignoreCase: true);

        entity.Name = request.Name;
        entity.IsEnabled = request.IsEnabled;
        entity.ProviderType = providerType;
        entity.PayloadFormat = payloadFormat;
        entity.EventTypeNames = request.EventTypeNames.ToList();
        entity.ProviderConfig = request.ProviderConfig;
        entity.TitleTemplate = request.TitleTemplate;
        entity.BodyTemplate = request.BodyTemplate;
        entity.RawJsonTemplate = request.RawJsonTemplate;
        entity.RuleFilter = request.RuleFilter?.ToRuleGroup();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
