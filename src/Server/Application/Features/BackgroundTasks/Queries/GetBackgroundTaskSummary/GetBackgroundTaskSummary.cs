using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTaskSummary;

public record GetBackgroundTaskSummaryQuery : IRequest<BackgroundTaskSummaryDto>;

public class GetBackgroundTaskSummaryQueryHandler : IRequestHandler<GetBackgroundTaskSummaryQuery, BackgroundTaskSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetBackgroundTaskSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BackgroundTaskSummaryDto> Handle(GetBackgroundTaskSummaryQuery request, CancellationToken cancellationToken)
    {
        var statusCounts = await _context.BackgroundTasks
            .GroupBy(t => t.Status)
            .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var taskTypeCounts = await _context.BackgroundTasks
            .GroupBy(t => t.Name)
            .Select(g => new TaskTypeCountDto { TaskName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        return new BackgroundTaskSummaryDto
        {
            TotalCount = statusCounts.Sum(s => s.Count),
            StatusCounts = statusCounts,
            TaskTypeCounts = taskTypeCounts
        };
    }
}
