using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Application.Features.Devices.Queries.GetDevice;

public record GetDeviceQuery(Guid Id) : IRequest<Device>;

public class GetDeviceQueryHandler : IRequestHandler<GetDeviceQuery, Device>
{
    private readonly IApplicationDbContext _context;

    public GetDeviceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Device> Handle(GetDeviceQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Devices
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
