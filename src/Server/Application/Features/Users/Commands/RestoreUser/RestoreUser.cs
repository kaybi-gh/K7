using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Users.Commands.RestoreUser;

[Authorize(Roles = Roles.Administrator)]
public record RestoreUserCommand : IRequest
{
    public required Guid UserId { get; init; }
}

public class RestoreUserCommandHandler(
    IApplicationDbContext context) : IRequestHandler<RestoreUserCommand>
{
    public async Task Handle(RestoreUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        Guard.Against.NotFound(request.UserId, user);

        user.DeletedAt = null;
        user.IsActive = true;

        await context.SaveChangesAsync(cancellationToken);
    }
}
