using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Queries.GetTwoFactorStatus;

public record GetTwoFactorStatusQuery : IRequest<TwoFactorStatusDto>;

public class GetTwoFactorStatusQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<GetTwoFactorStatusQuery, TwoFactorStatusDto>
{
    public async Task<TwoFactorStatusDto> Handle(GetTwoFactorStatusQuery request, CancellationToken cancellationToken)
    {
        var identityUserId = await ResolveIdentityUserIdAsync(cancellationToken);
        var status = await identityService.GetTwoFactorStatusAsync(identityUserId);

        return new TwoFactorStatusDto
        {
            IsEnabled = status.IsEnabled,
            HasAuthenticator = status.HasAuthenticator,
            RecoveryCodesLeft = status.RecoveryCodesLeft
        };
    }

    private async Task<string> ResolveIdentityUserIdAsync(CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null)
            throw new ForbiddenAccessException();

        if (!await identityService.HasPasswordAsync(user.IdentityUserId))
            throw new ForbiddenAccessException();

        return user.IdentityUserId;
    }
}
