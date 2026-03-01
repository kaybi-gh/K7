using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.Devices.Commands.AttachDeviceToCurrentUser;

public record AttachDeviceToCurrentUserCommand : IRequest<IResult>
{
    public required Guid DeviceId { get; init; }
}

public class AttachDeviceToCurrentUserCommandHandler : IRequestHandler<AttachDeviceToCurrentUserCommand, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public AttachDeviceToCurrentUserCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<IResult> Handle(AttachDeviceToCurrentUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.IdentityId))
        {
            return Results.Unauthorized();
        }

        var device = await _context.Devices
            .Include(d => d.Users)
            .SingleOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);

        if (device is null)
        {
            return Results.NotFound();
        }

        var domainUser = await _context.Users.SingleOrDefaultAsync(u => u.IdentityUserId == _user.IdentityId, cancellationToken);

        if (domainUser is null)
        {
            return Results.NotFound();
        }

        if (device.Users.Any(u => u.Id == domainUser.Id))
        {
            return Results.NoContent();
        }

        device.Users.Add(domainUser);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
