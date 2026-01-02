using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Application.Features.Devices.Queries.GetDevices;

public record GetDevicesQuery : IRequest<IEnumerable<Device>>;

public class GetDevicesQueryHandler : IRequestHandler<GetDevicesQuery, IEnumerable<Device>>
{
    private readonly IApplicationDbContext _context;

    public GetDevicesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Device>> Handle(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Devices
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeen)
            .ToListAsync(cancellationToken);
    }
}
