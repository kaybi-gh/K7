using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices;

namespace K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;

public record UpdateDeviceLastSeenCommand(Guid DeviceId) : IRequest;

public class UpdateDeviceLastSeenCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IIdentityService identityService)
    : IRequestHandler<UpdateDeviceLastSeenCommand>
{
    public async Task Handle(UpdateDeviceLastSeenCommand request, CancellationToken cancellationToken)
    {
        if (!await DeviceCallerAccess.CallerCanAccessAsync(
                context, user, identityService, request.DeviceId, cancellationToken))
            return;

        await context.Devices
            .Where(d => d.Id == request.DeviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeen, DateTimeOffset.UtcNow), cancellationToken);
    }
}
