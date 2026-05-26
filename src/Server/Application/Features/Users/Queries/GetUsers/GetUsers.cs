using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Users.Queries.GetUsers;

public record GetUsersResult(List<User> Users, Dictionary<Guid, Guid> AvatarPictureIds);

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

        var result = new List<User>();

        foreach (var user in domainUsers)
        {
            user.Role = Roles.Guest;

            if (user.IdentityUserId is not null)
            {
                user.Email = await _identityService.GetEmailAsync(user.IdentityUserId);
                user.UserName = await _identityService.GetUserNameAsync(user.IdentityUserId);
                var roles = await _identityService.GetRolesAsync(user.IdentityUserId);
                user.Role = roles.FirstOrDefault() ?? Roles.Guest;
            }

            if (request.Role is not null && user.Role != request.Role)
                continue;

            result.Add(user);
        }

        var userIds = result.Select(u => u.Id).ToList();
        var avatarMap = await _context.MetadataPictures
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken);

        return new GetUsersResult(result, avatarMap);
    }
}
