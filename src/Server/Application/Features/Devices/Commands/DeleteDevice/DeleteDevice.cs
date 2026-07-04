using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Devices.Commands.DeleteDevice;

public record DeleteDeviceCommand(Guid Id) : IRequest;

public class DeleteDeviceCommandHandler : IRequestHandler<DeleteDeviceCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteDeviceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Devices
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.Devices.Remove(entity);
        entity.AddDomainEvent(new DeviceDeletedEvent(entity));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
