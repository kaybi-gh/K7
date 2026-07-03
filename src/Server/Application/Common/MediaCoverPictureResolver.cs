using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common;

internal static class MediaCoverPictureResolver
{
    internal static async Task<Dictionary<Guid, Guid?>> GetCoverPictureIdsByMediaIdAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
            return [];

        var medias = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Include(m => m.Pictures)
            .Include(m => ((MusicTrack)m).Album)
                .ThenInclude(a => a.Pictures)
            .Include(m => ((SerieEpisode)m).Season)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieEpisode)m).Serie)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieSeason)m).Serie)
                .ThenInclude(s => s.Pictures)
            .ToListAsync(cancellationToken);

        return medias.ToDictionary(m => m.Id, m => m.GetCoverPictureId());
    }

    internal static string? ToSmallPictureUrl(Guid? pictureId) =>
        pictureId is Guid id ? $"/api/metadata-pictures/{id}?size=Small" : null;

    internal static async Task<IReadOnlyList<T>> EnrichTopItemsAsync<T>(
        IApplicationDbContext context,
        IReadOnlyList<T> items,
        Func<T, Guid> idSelector,
        Func<T, string?, T> withImageUrl,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return items;

        var ids = items.Select(idSelector).Distinct().ToList();
        var coverIds = await GetCoverPictureIdsByMediaIdAsync(context, ids, cancellationToken);

        return items
            .Select(item => withImageUrl(item, ToSmallPictureUrl(coverIds.GetValueOrDefault(idSelector(item)))))
            .ToList();
    }
}
