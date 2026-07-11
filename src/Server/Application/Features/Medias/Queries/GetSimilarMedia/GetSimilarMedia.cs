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
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media is null || media.Recommendations.Count == 0)
            return [];

        var recommendationPairs = media.Recommendations
            .SelectMany(rec => rec.RecommendedIds.Select(id => (rec.ProviderName, RecommendedId: id)))
            .ToList();

        if (recommendationPairs.Count == 0)
            return [];

        var pairSet = recommendationPairs.ToHashSet();
        var recommendedValues = recommendationPairs.Select(p => p.RecommendedId).Distinct().ToList();
        var providerNames = recommendationPairs.Select(p => p.ProviderName).Distinct().ToList();

        var matchedMediaIds = (await context.ExternalIds
            .AsNoTracking()
            .Where(e => e.MediaId != null
                && e.MediaId != request.MediaId
                && recommendedValues.Contains(e.Value)
                && providerNames.Contains(e.ProviderName))
            .Select(e => new { e.ProviderName, e.Value, MediaId = e.MediaId!.Value })
            .ToListAsync(cancellationToken))
            .Where(e => pairSet.Contains((e.ProviderName, e.Value)))
            .Select(e => e.MediaId)
            .Distinct()
            .Take(request.PageSize)
            .ToList();

        if (matchedMediaIds.Count == 0)
            return [];

        var items = await context.Medias
            .AsNoTracking()
            .Where(m => matchedMediaIds.Contains(m.Id))
            .IncludeMetadataTagsForMapping()
            .Include(m => m.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(m => m.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(m => m.Id);
        return matchedMediaIds
            .Where(itemsById.ContainsKey)
            .Select(id => itemsById[id].ToLiteMediaDto())
            .ToList();
    }
}
