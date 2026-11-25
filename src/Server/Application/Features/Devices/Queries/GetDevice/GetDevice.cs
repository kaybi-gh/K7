using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices;
using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Application.Features.Devices.Queries.GetDevice;

public record GetDeviceQuery(Guid Id) : IRequest<Device>;

public class GetDeviceQueryHandler : IRequestHandler<GetDeviceQuery, Device>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public GetDeviceQueryHandler(IApplicationDbContext context, IUser user, IIdentityService identityService)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
    }

    public async Task<Device> Handle(GetDeviceQuery request, CancellationToken cancellationToken)
    {
        await DeviceCallerAccess.EnsureCallerCanAccessAsync(
            _context, _user, _identityService, request.Id, cancellationToken);

        var entity = await _context.Devices
            .AsNoTracking()
            .Include(x => x.Users)
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity;
    }
}
