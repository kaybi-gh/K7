using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Common.Services;

public static class ContinueWatchingEpisodeSelector
{
    public static List<BaseMedia> DeduplicateBySerie(IReadOnlyList<BaseMedia> items)
    {
        var nonEpisodes = items.Where(i => i is not SerieEpisode).ToList();
        var selectedEpisodes = items
            .OfType<SerieEpisode>()
            .GroupBy(e => e.SerieId)
            .Select(SelectEpisodeForSerie)
            .ToList();

        return nonEpisodes
            .Concat(selectedEpisodes)
            .OrderByDescending(GetLastInteractedAt)
            .ToList();
    }

    private static SerieEpisode SelectEpisodeForSerie(IGrouping<Guid, SerieEpisode> group)
    {
        var episodes = group
            .OrderBy(e => e.Season?.SeasonNumber == 0 ? int.MaxValue : e.Season?.SeasonNumber ?? 0)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();

        var inProgress = episodes
            .Where(e => IsInProgress(e.UserMediaStates.FirstOrDefault()))
            .OrderByDescending(e => e.UserMediaStates.FirstOrDefault()?.LastInteractedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        return inProgress ?? episodes[0];
    }

    private static bool IsInProgress(UserMediaState? state) =>
        state is { IsCompleted: false }
        && (state.LastPlaybackPosition > 0 || state is { ProgressPercentage: > 0 and < 100 });

    private static DateTime? GetLastInteractedAt(BaseMedia item) =>
        item.UserMediaStates.FirstOrDefault()?.LastInteractedAt;
}
