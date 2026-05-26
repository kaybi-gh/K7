using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using FluentValidation.Results;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.UnlinkExternalLogin;

public record UnlinkExternalLoginCommand : IRequest
{
    public required string Provider { get; init; }
}

public class UnlinkExternalLoginCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<UnlinkExternalLoginCommand>
{
    public async Task Handle(UnlinkExternalLoginCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null)
            throw new ForbiddenAccessException();

        var hasPassword = await identityService.HasPasswordAsync(user.IdentityUserId);
        var externalLogins = await identityService.GetExternalLoginsAsync(user.IdentityUserId);

        var login = externalLogins.FirstOrDefault(l => l.LoginProvider == request.Provider);
        if (login is null)
            throw new NotFoundException(request.Provider, "External login");

        // Safety: must keep at least one login method
        var totalMethods = (hasPassword ? 1 : 0) + externalLogins.Count;
        if (totalMethods <= 1)
            throw new ValidationException([new ValidationFailure("Provider", "Cannot unlink the last login method.")]);

        await identityService.RemoveExternalLoginAsync(user.IdentityUserId, login.LoginProvider, login.ProviderKey);
    }
}
