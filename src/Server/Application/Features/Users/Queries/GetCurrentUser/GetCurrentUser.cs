using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<User>;

public class GetCurrentUserQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<GetCurrentUserQuery, User>
{
    public async Task<User> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id, message: "User is not authenticated.");

        var user = await context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
            .Include(u => u.MediaExclusions)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        Guard.Against.NotFound(userId, user);

        user.Role = Roles.Guest;
        if (user.IdentityUserId is not null)
        {
            user.Email = await identityService.GetEmailAsync(user.IdentityUserId);
            user.UserName = await identityService.GetUserNameAsync(user.IdentityUserId);
            var roles = await identityService.GetRolesAsync(user.IdentityUserId);
            user.Role = roles.FirstOrDefault() ?? Roles.Guest;
        }

        return user;
    }
}
