using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.ResetUserPassword;

[Authorize(Roles = Roles.Administrator)]
public record ResetUserPasswordCommand : IRequest
{
    public required Guid UserId { get; init; }
    public required string NewPassword { get; init; }
}

public class ResetUserPasswordCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<ResetUserPasswordCommand>
{
    public async Task Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        Guard.Against.NotFound(request.UserId, user);

        if (user.IdentityUserId is null)
            throw new InvalidOperationException("User has no identity account.");

        await identityService.ResetPasswordAsync(user.IdentityUserId, request.NewPassword);
    }
}
