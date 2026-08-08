using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ClientAppPasswords.Queries.GetClientAppPasswords;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetClientAppPasswordsQuery : IRequest<List<ClientAppPasswordDto>>;

public class GetClientAppPasswordsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetClientAppPasswordsQuery, List<ClientAppPasswordDto>>
{
    public async Task<List<ClientAppPasswordDto>> Handle(
        GetClientAppPasswordsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id!.Value;

        return await context.ClientAppPasswords
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Created)
            .Select(p => new ClientAppPasswordDto
            {
                Id = p.Id,
                Name = p.Name,
                CreatedAt = p.Created,
                LastUsedAt = p.LastUsedAt
            })
            .ToListAsync(cancellationToken);
    }
}
