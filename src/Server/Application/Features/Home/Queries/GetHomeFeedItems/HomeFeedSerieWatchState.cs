using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;

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
        Guid? sharedProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var serieIds = feedItems
            .Where(i => i.MediaType == MediaType.Serie)
            .Select(i => i.Id)
            .Distinct()
            .ToList();

        if (serieIds.Count == 0)
            return;

        var aggregated = await SerieWatchStateAggregator.AggregateAsync(
            context,
            serieIds,
            seasonIds: [],
            userId,
            sharedProfileId,
            cancellationToken);

        for (var i = 0; i < feedItems.Count; i++)
        {
            var item = feedItems[i];
            if (item.MediaType != MediaType.Serie)
                continue;

            if (!aggregated.TryGetValue(item.Id, out var state))
                continue;

            feedItems[i] = item with
            {
                Watched = state.IsCompleted,
                Progress = state.ProgressPercentage
            };
        }
    }
}
