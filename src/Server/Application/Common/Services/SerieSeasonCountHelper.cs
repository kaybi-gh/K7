using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

public static class SerieSeasonCountHelper
{
    public static IEnumerable<Guid> ExtractSerieIdsFromMedias(IEnumerable<BaseMedia> medias) =>
        medias.OfType<SerieEpisode>().Select(e => e.SerieId).Distinct();

    public static async Task<IReadOnlyDictionary<Guid, int>> GetCountsBySerieIdsAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> serieIds,
        CancellationToken cancellationToken = default)
    {
        var ids = serieIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, int>();

        var counts = await context.Medias
            .OfType<SerieSeason>()
            .AsNoTracking()
            .Where(s => ids.Contains(s.SerieId))
            .GroupBy(s => s.SerieId)
            .Select(g => new { SerieId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.SerieId, x => x.Count);
    }

    public static int ResolveCount(
        Guid serieId,
        Serie? serie,
        IReadOnlyDictionary<Guid, int>? serieSeasonCounts) =>
        serieSeasonCounts is not null && serieSeasonCounts.TryGetValue(serieId, out var count)
            ? count
            : serie?.Seasons?.Count ?? 1;
}
