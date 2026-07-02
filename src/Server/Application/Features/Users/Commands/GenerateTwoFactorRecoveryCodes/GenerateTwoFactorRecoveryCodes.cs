using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Commands.GenerateTwoFactorRecoveryCodes;

public record GenerateTwoFactorRecoveryCodesCommand : IRequest<RecoveryCodesDto>;

public class GenerateTwoFactorRecoveryCodesCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IAuthenticationSettings authSettings) : IRequestHandler<GenerateTwoFactorRecoveryCodesCommand, RecoveryCodesDto>
{
    public async Task<RecoveryCodesDto> Handle(GenerateTwoFactorRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        if (!authSettings.LocalSignInEnabled)
            throw new ForbiddenAccessException();

        var identityUserId = await ResolveIdentityUserIdAsync(cancellationToken);
        var codes = await identityService.GenerateRecoveryCodesAsync(identityUserId);

        return new RecoveryCodesDto { RecoveryCodes = codes };
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
