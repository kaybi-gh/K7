using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Queries.GetLoginMethods;

public record GetLoginMethodsQuery : IRequest<LoginMethodsDto>;

public class GetLoginMethodsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<GetLoginMethodsQuery, LoginMethodsDto>
{
    public async Task<LoginMethodsDto> Handle(GetLoginMethodsQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        if (user.IdentityUserId is null)
            return new LoginMethodsDto
            {
                HasPassword = false,
                CanRemovePassword = false,
                TwoFactorEnabled = false,
                RecoveryCodesLeft = 0,
                ExternalLogins = []
            };

        var hasPassword = await identityService.HasPasswordAsync(user.IdentityUserId);
        var externalLogins = await identityService.GetExternalLoginsAsync(user.IdentityUserId);
        var twoFactorStatus = await identityService.GetTwoFactorStatusAsync(user.IdentityUserId);

        var totalMethods = (hasPassword ? 1 : 0) + externalLogins.Count;

        return new LoginMethodsDto
        {
            HasPassword = hasPassword,
            CanRemovePassword = hasPassword && externalLogins.Count > 0,
            TwoFactorEnabled = twoFactorStatus.IsEnabled,
            RecoveryCodesLeft = twoFactorStatus.RecoveryCodesLeft,
            ExternalLogins = externalLogins.Select(l => new ExternalLoginDto
            {
                Provider = l.LoginProvider,
                ProviderDisplayName = l.ProviderDisplayName,
                CanUnlink = totalMethods > 1
            }).ToList()
        };
    }
}
