using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Application.Common.Security;

namespace MediaServer.Application.Features.BackgroundTasks.Queries.GetBackgroundTask;

[Authorize]
public record GetBackgroundTaskQuery(Guid Id) : IRequest<BackgroundTaskDto>;

public class GetBackgroundTaskQueryHandler : IRequestHandler<GetBackgroundTaskQuery, BackgroundTaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetBackgroundTaskQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<BackgroundTaskDto> Handle(GetBackgroundTaskQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.BackgroundTasks
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .ProjectTo<BackgroundTaskDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
