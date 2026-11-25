using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Devices.Commands.DeleteDevice;

public record DeleteDeviceCommand(Guid Id) : IRequest;

public class DeleteDeviceCommandHandler : IRequestHandler<DeleteDeviceCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public DeleteDeviceCommandHandler(IApplicationDbContext context, IUser user, IIdentityService identityService)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
    }

    public async Task Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
    {
        await DeviceCallerAccess.EnsureCallerCanAccessAsync(
            _context, _user, _identityService, request.Id, cancellationToken);

        var entity = await _context.Devices
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.Devices.Remove(entity);
        entity.AddDomainEvent(new DeviceDeletedEvent(entity));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
