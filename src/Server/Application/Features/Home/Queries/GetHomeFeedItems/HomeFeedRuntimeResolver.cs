using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal static class HomeFeedRuntimeResolver
{
    public static async Task<IReadOnlyList<HomeFeedItemDto>> ApplyAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<HomeFeedItemDto> items,
        CancellationToken cancellationToken)
    {
        var missing = items
            .Where(i => i.RuntimeMinutes is not > 0 && IsVideoHeroType(i.MediaType))
            .ToList();
        if (missing.Count == 0)
            return items as IReadOnlyList<HomeFeedItemDto> ?? items.ToList();

        var directIds = missing
            .Where(i => i.MediaType is MediaType.Movie or MediaType.SerieEpisode)
            .Select(i => i.Id)
            .ToHashSet();
        var parentIds = missing
            .Where(i => i.MediaType is MediaType.Serie or MediaType.SerieSeason)
            .Select(i => i.Id)
            .ToHashSet();

        var directMinutes = await LoadVideoMinutesAsync(context, directIds, cancellationToken);
        await FillEpisodeRuntimesAsync(context, missing, directMinutes, cancellationToken);

        Dictionary<Guid, int> serieMinutes = [];
        Dictionary<Guid, int> seasonMinutes = [];
        if (parentIds.Count > 0)
            (serieMinutes, seasonMinutes) = await LoadParentRuntimesAsync(context, parentIds, cancellationToken);

        return Merge(items, directMinutes, serieMinutes, seasonMinutes);
    }

    internal static IReadOnlyList<HomeFeedItemDto> Merge(
        IReadOnlyCollection<HomeFeedItemDto> items,
        IReadOnlyDictionary<Guid, int> directMinutes,
        IReadOnlyDictionary<Guid, int> serieMinutes,
        IReadOnlyDictionary<Guid, int> seasonMinutes)
    {
        var result = new List<HomeFeedItemDto>(items.Count);
        foreach (var item in items)
        {
            if (item.RuntimeMinutes is > 0)
            {
                result.Add(item);
                continue;
            }

            var minutes = item.MediaType switch
            {
                MediaType.Movie or MediaType.SerieEpisode => Get(directMinutes, item.Id),
                MediaType.Serie => Get(serieMinutes, item.Id),
                MediaType.SerieSeason => Get(seasonMinutes, item.Id) ?? Get(serieMinutes, item.Id),
                _ => null
            };

            result.Add(minutes is > 0 ? item with { RuntimeMinutes = minutes } : item);
        }

        return result;
    }

    private static async Task FillEpisodeRuntimesAsync(
        IApplicationDbContext context,
        List<HomeFeedItemDto> missing,
        Dictionary<Guid, int> directMinutes,
        CancellationToken cancellationToken)
    {
        var episodeIds = missing
            .Where(i => i.MediaType == MediaType.SerieEpisode && !directMinutes.ContainsKey(i.Id))
            .Select(i => i.Id)
            .ToHashSet();
        if (episodeIds.Count == 0)
            return;

        var runtimes = await context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Where(e => episodeIds.Contains(e.Id) && e.Runtime > 0)
            .Select(e => new { e.Id, e.Runtime })
            .ToListAsync(cancellationToken);

        foreach (var row in runtimes)
        {
            if (row.Runtime is > 0)
                directMinutes[row.Id] = row.Runtime.Value;
        }
    }

    private static async Task<(Dictionary<Guid, int> SerieMinutes, Dictionary<Guid, int> SeasonMinutes)> LoadParentRuntimesAsync(
        IApplicationDbContext context,
        HashSet<Guid> parentIds,
        CancellationToken cancellationToken)
    {
        var episodes = await context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Where(e => parentIds.Contains(e.SerieId) || parentIds.Contains(e.SeasonId))
            .Select(e => new { e.Id, e.SerieId, e.SeasonId, e.Runtime })
            .ToListAsync(cancellationToken);

        var serieMinutes = FirstPositiveBy(episodes, e => e.SerieId, e => e.Runtime);
        var seasonMinutes = FirstPositiveBy(episodes, e => e.SeasonId, e => e.Runtime);

        var unresolvedParents = parentIds
            .Where(id => !serieMinutes.ContainsKey(id) && !seasonMinutes.ContainsKey(id))
            .ToHashSet();
        if (unresolvedParents.Count == 0)
            return (serieMinutes, seasonMinutes);

        var fallbackEpisodeIds = episodes
            .Where(e => unresolvedParents.Contains(e.SerieId) || unresolvedParents.Contains(e.SeasonId))
            .Select(e => e.Id)
            .ToHashSet();
        var fileMinutes = await LoadVideoMinutesAsync(context, fallbackEpisodeIds, cancellationToken);
        if (fileMinutes.Count == 0)
            return (serieMinutes, seasonMinutes);

        foreach (var episode in episodes)
        {
            if (!fileMinutes.TryGetValue(episode.Id, out var minutes))
                continue;

            serieMinutes.TryAdd(episode.SerieId, minutes);
            seasonMinutes.TryAdd(episode.SeasonId, minutes);
        }

        return (serieMinutes, seasonMinutes);
    }

    private static async Task<Dictionary<Guid, int>> LoadVideoMinutesAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
            return [];

        var idSet = mediaIds as HashSet<Guid> ?? mediaIds.ToHashSet();

        var local = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId.HasValue && idSet.Contains(f.MediaId.Value) && f.FileMetadata is VideoFileMetadata)
            .Select(f => new
            {
                MediaId = f.MediaId!.Value,
                Seconds = (f.FileMetadata as VideoFileMetadata)!.Duration.TotalSeconds
            })
            .ToListAsync(cancellationToken);

        var remote = await context.RemoteIndexedFiles
            .AsNoTracking()
            .Where(f => idSet.Contains(f.MediaId) && f.Duration != null)
            .Select(f => new { f.MediaId, Seconds = f.Duration!.Value.TotalSeconds })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var row in local)
            TryAddMinutes(result, row.MediaId, row.Seconds);

        foreach (var row in remote)
            TryAddMinutes(result, row.MediaId, row.Seconds);

        return result;
    }

    private static Dictionary<Guid, int> FirstPositiveBy<T>(
        IReadOnlyList<T> rows,
        Func<T, Guid> keySelector,
        Func<T, int?> valueSelector)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var row in rows)
        {
            var minutes = valueSelector(row);
            if (minutes is not > 0)
                continue;
            result.TryAdd(keySelector(row), minutes.Value);
        }

        return result;
    }

    private static void TryAddMinutes(Dictionary<Guid, int> target, Guid mediaId, double seconds)
    {
        if (target.ContainsKey(mediaId) || seconds < 60)
            return;

        target[mediaId] = (int)(seconds / 60);
    }

    private static int? Get(IReadOnlyDictionary<Guid, int> source, Guid id) =>
        source.TryGetValue(id, out var value) ? value : null;

    private static bool IsVideoHeroType(MediaType type) =>
        type is MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;
}
