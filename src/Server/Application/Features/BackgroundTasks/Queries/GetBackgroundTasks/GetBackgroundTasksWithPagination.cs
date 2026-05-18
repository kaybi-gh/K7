using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTasksWithPagination;

//[Authorize]
public record GetBackgroundTasksWithPaginationQuery : IRequest<PaginatedList<BackgroundTask>>
{
    public Guid[]? Ids { get; init; }
    public string[]? Names { get; init; }
    public EnumHashSetQueryParam<BackgroundTaskStatus>? Status { get; init; }
    public EnumHashSetQueryParam<BackgroundTaskPriority>? Priority { get; init; }
    public string? SortBy { get; init; }
    public bool? SortDescending { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetBackgroundTasksWithPaginationQueryHandler : IRequestHandler<GetBackgroundTasksWithPaginationQuery, PaginatedList<BackgroundTask>>
{
    private readonly IApplicationDbContext _context;

    public GetBackgroundTasksWithPaginationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<BackgroundTask>> Handle(GetBackgroundTasksWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.BackgroundTasks.AsQueryable();

        query = ApplyFilters(request, query);
        var orderedQuery = ApplySorting(request, query);
        return await orderedQuery.PaginatedListAsync(request.PageNumber, request.PageSize);
    }

    private static IQueryable<BackgroundTask> ApplyFilters(GetBackgroundTasksWithPaginationQuery request, IQueryable<BackgroundTask> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.Names?.Length > 0)
        {
            query = query.Where(x => request.Names.Contains(x.Name));
        }

        if (request.Status?.Count > 0)
        {
            query = query.Where(x => request.Status.Contains(x.Status));
        }

        if (request.Priority?.Count > 0)
        {
            query = query.Where(x => request.Priority.Contains(x.Priority));
        }

        return query;
    }

    private static IOrderedQueryable<BackgroundTask> ApplySorting(GetBackgroundTasksWithPaginationQuery request, IQueryable<BackgroundTask> query)
    {
        var descending = request.SortDescending ?? true;

        return request.SortBy?.ToLowerInvariant() switch
        {
            "date" => descending
                ? query.OrderByDescending(x => x.Created)
                : query.OrderBy(x => x.Created),
            "duration" => descending
                ? query.OrderByDescending(x => x.CompletedAt != null && x.StartedAt != null ? x.CompletedAt - x.StartedAt : null)
                : query.OrderBy(x => x.CompletedAt != null && x.StartedAt != null ? x.CompletedAt - x.StartedAt : null),
            "name" => descending
                ? query.OrderByDescending(x => x.Name)
                : query.OrderBy(x => x.Name),
            _ => descending
                ? query.OrderByDescending(x => x.LastModified)
                : query.OrderBy(x => x.LastModified)
        };
    }
}
