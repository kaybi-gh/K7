using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTask;

[Authorize]
public record GetBackgroundTaskQuery(Guid Id) : IRequest<BackgroundTask>;

public class GetBackgroundTaskQueryHandler : IRequestHandler<GetBackgroundTaskQuery, BackgroundTask>
{
    private readonly IApplicationDbContext _context;

    public GetBackgroundTaskQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BackgroundTask> Handle(GetBackgroundTaskQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
