using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Devices.Commands.UpdateDevice;

public record UpdateDeviceCommand : IRequest
{
    public Guid Id { get; init; }
}

public class UpdateDeviceCommandHandler : IRequestHandler<UpdateDeviceCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDeviceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateDeviceCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Devices
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        // TODO - Implement
        await _context.SaveChangesAsync(cancellationToken);
    }
}
