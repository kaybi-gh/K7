using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using FluentValidation.Results;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.SetPassword;

public record SetPasswordCommand : IRequest
{
    public required string NewPassword { get; init; }
}

public class SetPasswordCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IAuthenticationSettings authSettings) : IRequestHandler<SetPasswordCommand>
{
    public async Task Handle(SetPasswordCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        if (!authSettings.LocalSignInEnabled)
            throw new ForbiddenAccessException();

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null)
            throw new ForbiddenAccessException();

        var hasPassword = await identityService.HasPasswordAsync(user.IdentityUserId);
        if (hasPassword)
            throw new ValidationException([new ValidationFailure("NewPassword", "Account already has a password. Use change password instead.")]);

        await identityService.SetPasswordAsync(user.IdentityUserId, request.NewPassword);
    }
}
