using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;

namespace K7.Server.Application.Features.Notifications.Queries.GetNotificationRules;

[Authorize(Roles = Roles.Administrator)]
public record GetNotificationRulesQuery : IRequest<IEnumerable<NotificationRule>>;

public class GetNotificationRulesQueryHandler : IRequestHandler<GetNotificationRulesQuery, IEnumerable<NotificationRule>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationRulesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<NotificationRule>> Handle(GetNotificationRulesQuery request, CancellationToken cancellationToken)
    {
        return await _context.NotificationRules
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }
}
