using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Users.Queries.GetCurrentUser;

public record GetCurrentUserResult(User User, Guid? AvatarPictureId);

public record GetCurrentUserQuery : IRequest<GetCurrentUserResult>;

public class GetCurrentUserQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<GetCurrentUserQuery, GetCurrentUserResult>
{
    public async Task<GetCurrentUserResult> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id, message: "User is not authenticated.");

        var user = await context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
            .Include(u => u.MediaExclusions)
            .AsSplitQuery()
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

        var avatarPictureId = await context.MetadataPictures
            .Where(p => p.UserId == userId && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new GetCurrentUserResult(user, avatarPictureId);
    }
}
