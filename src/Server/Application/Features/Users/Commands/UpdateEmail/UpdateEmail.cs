using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.UpdateEmail;

public record UpdateEmailCommand : IRequest
{
    public required string Email { get; init; }
    public required string CurrentPassword { get; init; }
}

public class UpdateEmailCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<UpdateEmailCommand>
{
    public async Task Handle(UpdateEmailCommand request, CancellationToken cancellationToken)
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

        await identityService.UpdateEmailAsync(user.IdentityUserId, request.Email);
    }
}
