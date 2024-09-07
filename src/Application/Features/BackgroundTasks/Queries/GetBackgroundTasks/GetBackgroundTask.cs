using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Application.Common.Interfaces;

namespace MediaServer.Application.Features.BackgroundTasks.Queries.GetBackgroundTasks;

//[Authorize]
public record GetBackgroundTasksQuery : IRequest<IEnumerable<BackgroundTaskDto>>;

public class GetBackgroundTasksQueryHandler : IRequestHandler<GetBackgroundTasksQuery, IEnumerable<BackgroundTaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetBackgroundTasksQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<BackgroundTaskDto>> Handle(GetBackgroundTasksQuery request, CancellationToken cancellationToken)
    {
        return await _context.BackgroundTasks
            .AsNoTracking()
            .ProjectTo<BackgroundTaskDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
