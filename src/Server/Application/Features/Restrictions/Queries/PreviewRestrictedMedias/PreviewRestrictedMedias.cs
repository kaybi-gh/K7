using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Restrictions;

namespace K7.Server.Application.Features.Restrictions.Queries.PreviewRestrictedMedias;

public record PreviewRestrictedMediasQuery(Guid ProfileId) : IRequest<List<RestrictedMediaPreviewDto>>;

public class PreviewRestrictedMediasQueryHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewRestrictedMediasQuery, List<RestrictedMediaPreviewDto>>
{
    public async Task<List<RestrictedMediaPreviewDto>> Handle(PreviewRestrictedMediasQuery request, CancellationToken cancellationToken)
    {
        var profile = await context.ContentRestrictionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return [];

        var query = context.Medias
            .Where(x => x.IndexedFiles.Any())
            .AsNoTracking();

        var restricted = ContentRestrictionEvaluator.GetRestricted(query, profile);

        return await restricted
            .OrderBy(m => m.Title)
            .Take(200)
            .Select(m => new RestrictedMediaPreviewDto
            {
                Id = m.Id,
                Title = m.Title,
                Type = m.Type,
                ReleaseYear = m.ReleaseDate != null ? m.ReleaseDate.Value.Year : null
            })
            .ToListAsync(cancellationToken);
    }
}
