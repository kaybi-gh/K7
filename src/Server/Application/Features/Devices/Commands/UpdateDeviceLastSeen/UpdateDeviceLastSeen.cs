using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;

public record UpdateDeviceLastSeenCommand(Guid DeviceId) : IRequest;

public class UpdateDeviceLastSeenCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateDeviceLastSeenCommand>
{
    public async Task Handle(UpdateDeviceLastSeenCommand request, CancellationToken cancellationToken)
    {
        await context.Devices
            .Where(d => d.Id == request.DeviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeen, DateTimeOffset.UtcNow), cancellationToken);
    }
}
