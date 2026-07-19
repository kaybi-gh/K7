using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Queries.GetUsers;

public record GetUsersResult(List<UserDto> Users, Dictionary<Guid, Guid> AvatarPictureIds);

[Authorize(Roles = Roles.Administrator)]
public record GetUsersQuery : IRequest<GetUsersResult>
{
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
}

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, GetUsersResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<GetUsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
            .Include(u => u.MediaExclusions)
            .AsSplitQuery()
            .AsNoTracking()
            .AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        var domainUsers = await query
            .OrderBy(u => u.Created)
            .ToListAsync(cancellationToken);

        var identityUserIds = domainUsers
            .Where(user => user.IdentityUserId is not null)
            .Select(user => user.IdentityUserId!)
            .ToList();
        var userNames = await _identityService.GetUserNamesAsync(identityUserIds);
        var emails = await _identityService.GetEmailsAsync(identityUserIds);
        var rolesByIdentityUserId = await _identityService.GetRolesAsync(identityUserIds);
        var result = domainUsers
            .Select(user =>
            {
                var role = user.IdentityUserId is { } identityUserId
                    && rolesByIdentityUserId.TryGetValue(identityUserId, out var roles)
                    ? roles.FirstOrDefault() ?? Roles.Guest
                    : Roles.Guest;

                return user.ToUserDto() with
                {
                    Email = user.IdentityUserId is { } emailIdentityUserId
                        ? emails.GetValueOrDefault(emailIdentityUserId)
                        : user.Email,
                    UserName = user.IdentityUserId is { } userNameIdentityUserId
                        ? userNames.GetValueOrDefault(userNameIdentityUserId)
                        : user.UserName,
                    Role = role,
                    IsGuest = role == Roles.Guest
                };
            })
            .Where(user => request.Role is null || user.Role == request.Role)
            .ToList();

        var userIds = result.Select(u => u.Id).ToList();
        var avatarMap = await _context.MetadataPictures
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken);

        return new GetUsersResult(result, avatarMap);
    }
}
