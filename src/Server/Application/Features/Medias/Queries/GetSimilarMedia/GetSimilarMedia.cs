using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.GetSimilarMedia;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetSimilarMediaQuery : IRequest<List<LiteMediaDto>>
{
    public required Guid MediaId { get; init; }
    public int PageSize { get; init; } = 20;
}

public class GetSimilarMediaQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSimilarMediaQuery, List<LiteMediaDto>>
{
    public async Task<List<LiteMediaDto>> Handle(GetSimilarMediaQuery request, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.Recommendations)
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media is null)
            return [];

        var results = new List<BaseMedia>();

        // Strategy 1: Find local media matching persisted recommendation IDs
        var recommendations = media.Recommendations;
        if (recommendations.Count > 0)
        {
            foreach (var rec in recommendations)
            {
                var matchingMedia = await context.Medias
                    .AsNoTracking()
                    .IncludeMetadataTagsForMapping()
                    .Include(m => m.Pictures)
                        .ThenInclude(p => p.Variants)
                    .Include(m => m.UserMediaStates)
                    .Where(m => m.Id != request.MediaId)
                    .Where(m => m.ExternalIds.Any(e =>
                        e.ProviderName == rec.ProviderName &&
                        rec.RecommendedIds.Contains(e.Value)))
                    .Take(request.PageSize)
                    .ToListAsync(cancellationToken);

                results.AddRange(matchingMedia);
            }
        }

        return results
            .DistinctBy(m => m.Id)
            .Take(request.PageSize)
            .Select(m => m.ToLiteMediaDto())
            .ToList();
    }
}
