using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Features.Devices.Commands.AttachDeviceToCurrentUser;

public record AttachDeviceToCurrentUserCommand : IRequest<HttpContentResult>
{
    public required Guid DeviceId { get; init; }
}

public class AttachDeviceToCurrentUserCommandHandler : IRequestHandler<AttachDeviceToCurrentUserCommand, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public AttachDeviceToCurrentUserCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<HttpContentResult> Handle(AttachDeviceToCurrentUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.IdentityId))
        {
            return new EmptyHttpContentResult(401);
        }

        var device = await _context.Devices
            .Include(d => d.Users)
            .SingleOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);

        if (device is null)
        {
            return new EmptyHttpContentResult(404);
        }

        var domainUser = await _context.Users.SingleOrDefaultAsync(u => u.IdentityUserId == _user.IdentityId, cancellationToken);

        if (domainUser is null)
        {
            return new EmptyHttpContentResult(404);
        }

        if (device.Users.Any(u => u.Id == domainUser.Id))
        {
            return new EmptyHttpContentResult(204);
        }

        device.Users.Add(domainUser);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmptyHttpContentResult(204);
    }
}
