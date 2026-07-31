using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

/// <summary>
/// Home top-level rows show Serie cards, but watch state is stored on episodes.
/// Aggregate episode states onto serie feed items so Watched/Progress stay accurate.
/// </summary>
internal static class HomeFeedSerieWatchState
{
    public static async Task ApplyAsync(
        IApplicationDbContext context,
        IList<HomeFeedItemDto> feedItems,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var serieIds = feedItems
            .Where(i => i.MediaType == MediaType.Serie)
            .Select(i => i.Id)
            .Distinct()
            .ToList();

        if (serieIds.Count == 0)
            return;

        var episodeRows = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => serieIds.Contains(e.SerieId))
            .Select(e => new { e.Id, e.SerieId })
            .ToListAsync(cancellationToken);

        if (episodeRows.Count == 0)
            return;

        var episodeIds = episodeRows.Select(e => e.Id).ToList();
        var statesByMediaId = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && episodeIds.Contains(s.MediaId))
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        var statesBySerie = episodeRows
            .GroupBy(e => e.SerieId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => statesByMediaId.GetValueOrDefault(e.Id)).ToList());

        for (var i = 0; i < feedItems.Count; i++)
        {
            var item = feedItems[i];
            if (item.MediaType != MediaType.Serie)
                continue;

            if (!statesBySerie.TryGetValue(item.Id, out var states))
                continue;

            var aggregated = SeasonWatchStateHelper.AggregateFromEpisodeStates(states);
            if (aggregated is null)
                continue;

            feedItems[i] = item with
            {
                Watched = aggregated.IsCompleted,
                Progress = aggregated.ProgressPercentage
            };
        }
    }
}
