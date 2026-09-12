using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Queries.GetUserScrobblerAccounts;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetUserScrobblerAccountsQuery : IRequest<List<UserScrobblerAccountDto>>;

public class GetUserScrobblerAccountsQueryHandler(
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector)
    : IRequestHandler<GetUserScrobblerAccountsQuery, List<UserScrobblerAccountDto>>
{
    public async Task<List<UserScrobblerAccountDto>> Handle(
        GetUserScrobblerAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = user.Id!.Value;
        var accounts = await context.UserScrobblerAccounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Created)
            .ToListAsync(cancellationToken);

        return accounts.Select(account =>
        {
            try
            {
                return account.ToDto(protector.Unprotect(account.ConfigJson));
            }
            catch
            {
                return account.ToDto();
            }
        }).ToList();
    }
}
