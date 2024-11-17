using K7.Server.Application.Common.Models.Dtos;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.BackgroundTasks.Queries.GetBackgroundTasksWithPagination;

//[Authorize]
public record GetBackgroundTasksWithPaginationQuery : IRequest<PaginatedList<BackgroundTaskDto>>
{
    public Guid[]? Ids { get; init; }
    public EnumHashSetQueryParam<BackgroundTaskStatus>? Status { get; init; }
    public EnumHashSetQueryParam<BackgroundTaskPriority>? Priority { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetBackgroundTasksWithPaginationQueryHandler : IRequestHandler<GetBackgroundTasksWithPaginationQuery, PaginatedList<BackgroundTaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetBackgroundTasksWithPaginationQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedList<BackgroundTaskDto>> Handle(GetBackgroundTasksWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var query = _context.BackgroundTasks.AsQueryable();

        query = ApplyFilters(request, query);
        var orderedQuery = query.OrderByDescending(x => x.Priority).ThenBy(x => x.Created); // TODO - Add custom sorting?
        var page = await orderedQuery.PaginatedListAsync(request.PageNumber, request.PageSize);

        List<BackgroundTaskDto> dtos = page.Items
            .Select(_mapper.Map<BackgroundTaskDto>)
            .ToList();

        return new PaginatedList<BackgroundTaskDto>(dtos.AsReadOnly(), page.TotalCount, request.PageNumber, request.PageSize);
    }

    private static IQueryable<BackgroundTask> ApplyFilters(GetBackgroundTasksWithPaginationQuery request, IQueryable<BackgroundTask> query)
    {
        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
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
}
