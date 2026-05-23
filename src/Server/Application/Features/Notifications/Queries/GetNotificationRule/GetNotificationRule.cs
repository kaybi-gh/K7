using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;

namespace K7.Server.Application.Features.Notifications.Queries.GetNotificationRule;

[Authorize(Roles = Roles.Administrator)]
public record GetNotificationRuleQuery(Guid Id) : IRequest<NotificationRule>;

public class GetNotificationRuleQueryHandler : IRequestHandler<GetNotificationRuleQuery, NotificationRule>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationRuleQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationRule> Handle(GetNotificationRuleQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.NotificationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        return entity;
    }
}
