using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Notifications.Commands.DeleteNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record DeleteNotificationRuleCommand(Guid Id) : IRequest;

public class DeleteNotificationRuleCommandHandler : IRequestHandler<DeleteNotificationRuleCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteNotificationRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.AddDomainEvent(new NotificationRuleDeletedEvent(entity));
        _context.NotificationRules.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
