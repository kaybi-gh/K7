using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Queries.GetUsers;

[Authorize(Roles = Roles.Administrator)]
public record GetUsersQuery : IRequest<List<User>>
{
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
}

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<User>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<User>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
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

        return result;
    }
}
