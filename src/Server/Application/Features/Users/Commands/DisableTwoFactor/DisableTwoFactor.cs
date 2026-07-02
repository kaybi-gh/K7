using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Users.Commands.DisableTwoFactor;

public record DisableTwoFactorCommand : IRequest;

public class DisableTwoFactorCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IAuthenticationSettings authSettings) : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        if (!authSettings.LocalSignInEnabled)
            throw new ForbiddenAccessException();

        var identityUserId = await ResolveIdentityUserIdAsync(cancellationToken);
        await identityService.DisableTwoFactorAsync(identityUserId);
    }

    private async Task<string> ResolveIdentityUserIdAsync(CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null || !await identityService.HasPasswordAsync(user.IdentityUserId))
            throw new ForbiddenAccessException();

        return user.IdentityUserId;
    }
}
