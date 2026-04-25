using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Users.Queries.GetSelfMediaExclusions;

public record GetSelfMediaExclusionsQuery : IRequest<IReadOnlyList<LiteMediaDto>>
{
    public bool IncludeAdminExcluded { get; init; }
}

public class GetSelfMediaExclusionsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetSelfMediaExclusionsQuery, IReadOnlyList<LiteMediaDto>>
{
    public async Task<IReadOnlyList<LiteMediaDto>> Handle(GetSelfMediaExclusionsQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);

        var mediaIds = await context.UserMediaExclusions
            .Where(e => e.UserId == userId && e.IsSelfExcluded && (!e.IsAdminExcluded || request.IncludeAdminExcluded))
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
