using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Users;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.VerifyTwoFactorSetup;

public record VerifyTwoFactorSetupCommand : IRequest<RecoveryCodesDto>
{
    public required string Code { get; init; }
}

public class VerifyTwoFactorSetupCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IAuthenticationSettings authSettings) : IRequestHandler<VerifyTwoFactorSetupCommand, RecoveryCodesDto>
{
    public async Task<RecoveryCodesDto> Handle(VerifyTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        if (!authSettings.LocalSignInEnabled)
            throw new ForbiddenAccessException();

        var identityUserId = await ResolveIdentityUserIdAsync(cancellationToken);

        try
        {
            var codes = await identityService.VerifyAndEnableTwoFactorAsync(identityUserId, request.Code);
            return new RecoveryCodesDto { RecoveryCodes = codes };
        }
        catch (InvalidOperationException)
        {
            throw new ValidationException([new ValidationFailure(nameof(request.Code), "Invalid verification code.")]);
        }
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
