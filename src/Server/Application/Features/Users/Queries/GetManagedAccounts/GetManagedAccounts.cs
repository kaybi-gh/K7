using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Queries.GetManagedAccounts;

public record GetManagedAccountsResult(List<ManagedAccountDto> Accounts, Dictionary<Guid, Guid> AvatarPictureIds);

/// <summary>
/// Accounts the current user is a guardian of and can open a device session for right now.
/// </summary>
public record GetManagedAccountsQuery : IRequest<GetManagedAccountsResult>;

public class GetManagedAccountsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<GetManagedAccountsQuery, GetManagedAccountsResult>
{
    public async Task<GetManagedAccountsResult> Handle(GetManagedAccountsQuery request, CancellationToken cancellationToken)
    {
        var guardianUserId = await currentUser.GetIdAsync(cancellationToken);
        if (guardianUserId is null)
            return new GetManagedAccountsResult([], []);

        var managed = await context.UserGuardians
            .AsNoTracking()
            .Where(g => g.GuardianUserId == guardianUserId
                && g.User.IsActive
                && g.User.IdentityUserId != null
                && g.User.PeerServerId == null)
            .Select(g => g.User)
            .ToListAsync(cancellationToken);

        if (managed.Count == 0)
            return new GetManagedAccountsResult([], []);

        var identityUserIds = managed.Select(u => u.IdentityUserId!).ToList();
        var userNames = await identityService.GetUserNamesAsync(identityUserIds);
        var rolesByIdentityUserId = await identityService.GetRolesAsync(identityUserIds);

        var accounts = managed
            // Same rule as when the session is opened: only ordinary user accounts.
            .Where(u => rolesByIdentityUserId.TryGetValue(u.IdentityUserId!, out var roles)
                && roles.Contains(Roles.User)
                && !roles.Contains(Roles.Administrator))
            .Select(u => new ManagedAccountDto
            {
                Id = u.Id,
                UserName = userNames.GetValueOrDefault(u.IdentityUserId!),
                DisplayName = u.DisplayName,
                HasPin = u.PinHash is not null
            })
            .OrderBy(a => a.DisplayName ?? a.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var accountIds = accounts.Select(a => a.Id).ToList();
        var avatarPictureIds = await context.MetadataPictures
            .Where(p => p.UserId != null && accountIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken);

        return new GetManagedAccountsResult(accounts, avatarPictureIds);
    }
}
