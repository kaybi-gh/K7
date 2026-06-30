using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTaskSummary;

public record GetBackgroundTaskSummaryQuery : IRequest<BackgroundTaskSummaryDto>
{
    public EnumHashSetQueryParam<BackgroundTaskStatus>? StatusFilter { get; init; }
    public string[]? NamesFilter { get; init; }
}

public class GetBackgroundTaskSummaryQueryHandler : IRequestHandler<GetBackgroundTaskSummaryQuery, BackgroundTaskSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetBackgroundTaskSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BackgroundTaskSummaryDto> Handle(GetBackgroundTaskSummaryQuery request, CancellationToken cancellationToken)
    {
        var statusCounts = await ApplyFilters(_context.BackgroundTasks, names: request.NamesFilter)
            .GroupBy(t => t.Status)
            .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var taskTypeCounts = await ApplyFilters(_context.BackgroundTasks, statuses: request.StatusFilter)
            .GroupBy(t => t.Name)
            .Select(g => new TaskTypeCountDto { TaskName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var totalCount = await ApplyFilters(_context.BackgroundTasks, statuses: request.StatusFilter, names: request.NamesFilter)
            .CountAsync(cancellationToken);

        return new BackgroundTaskSummaryDto
        {
            TotalCount = totalCount,
            StatusCounts = statusCounts,
            TaskTypeCounts = taskTypeCounts
        };
    }

    private static IQueryable<BackgroundTask> ApplyFilters(
        IQueryable<BackgroundTask> query,
        EnumHashSetQueryParam<BackgroundTaskStatus>? statuses = null,
        string[]? names = null)
    {
        if (statuses?.Count > 0)
            query = query.Where(t => statuses.Contains(t.Status));

        if (names?.Length > 0)
            query = query.Where(t => names.Contains(t.Name));

        return query;
    }
}
