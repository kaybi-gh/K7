using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Users.Queries.GetSelfMediaExclusions;

public record GetSelfMediaExclusionsQuery : IRequest<IReadOnlyList<LiteMediaDto>>;

public class GetSelfMediaExclusionsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<GetSelfMediaExclusionsQuery, IReadOnlyList<LiteMediaDto>>
{
    public async Task<IReadOnlyList<LiteMediaDto>> Handle(GetSelfMediaExclusionsQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var isAdmin = currentUser.IdentityId is not null
            && await identityService.IsInRoleAsync(currentUser.IdentityId, Roles.Administrator);

        var exclusions = context.UserMediaExclusions.Where(e => e.UserId == userId);
        exclusions = isAdmin
            ? exclusions.Where(e => e.IsSelfExcluded || e.IsAdminExcluded)
            : exclusions.Where(e => e.IsSelfExcluded && !e.IsAdminExcluded);

        var mediaIds = await exclusions
            .Select(e => e.MediaId)
            .ToListAsync(cancellationToken);

        if (mediaIds.Count == 0)
            return [];

        var medias = await context.Medias
            .Where(m => mediaIds.Contains(m.Id))
            .Include(m => m.Pictures)
                .ThenInclude(p => p.Variants)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return medias.Select(m => m.ToLiteMediaDto()).ToList();
    }
}
