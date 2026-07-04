using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.DeleteAccount;

public record DeleteAccountCommand : IRequest
{
    public string? CurrentPassword { get; init; }
}

public class DeleteAccountCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<DeleteAccountCommand>
{
    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        // Block guest account deletion
        if (user.Role == Roles.Guest)
            throw new ForbiddenAccessException();

        // Verify password if user has one
        if (user.IdentityUserId is not null && request.CurrentPassword is not null)
        {
            var hasPassword = await identityService.HasPasswordAsync(user.IdentityUserId);
            if (hasPassword)
            {
                var passwordValid = await identityService.VerifyPasswordAsync(user.IdentityUserId, request.CurrentPassword);
                if (!passwordValid)
                    throw new ValidationException([new ValidationFailure("CurrentPassword", "Current password is incorrect.")]);
            }
        }

        user.DeletedAt = DateTimeOffset.UtcNow;
        user.IsActive = false;

        await context.SaveChangesAsync(cancellationToken);
    }
}
