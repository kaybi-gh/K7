using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using FluentValidation.Results;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.RemovePassword;

public record RemovePasswordCommand : IRequest
{
    public required string CurrentPassword { get; init; }
}

public class RemovePasswordCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<RemovePasswordCommand>
{
    public async Task Handle(RemovePasswordCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null)
            throw new ForbiddenAccessException();

        var passwordValid = await identityService.VerifyPasswordAsync(user.IdentityUserId, request.CurrentPassword);
        if (!passwordValid)
            throw new ValidationException([new ValidationFailure("CurrentPassword", "Current password is incorrect.")]);

        // Safety check: ensure user has at least one external login remaining
        var externalLogins = await identityService.GetExternalLoginsAsync(user.IdentityUserId);
        if (externalLogins.Count == 0)
            throw new ValidationException([new ValidationFailure("CurrentPassword", "Cannot remove password without an alternative login method.")]);

        await identityService.RemovePasswordAsync(user.IdentityUserId);
    }
}
