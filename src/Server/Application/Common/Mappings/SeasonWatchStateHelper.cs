using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

internal static class SeasonWatchStateHelper
{
    public static UserMediaStateDto? AggregateFromEpisodes(IReadOnlyList<SerieEpisode> episodes)
    {
        if (episodes.Count == 0)
            return null;

        var completedCount = 0;
        var totalProgress = 0.0;
        DateTime? lastInteractedAt = null;

        foreach (var episode in episodes)
        {
            var state = episode.UserMediaStates.FirstOrDefault();
            if (state?.IsCompleted == true)
            {
                completedCount++;
                totalProgress += 100;
            }
            else
            {
                totalProgress += state?.ProgressPercentage ?? 0;
            }

            if (state?.LastInteractedAt is not null
                && (lastInteractedAt is null || state.LastInteractedAt > lastInteractedAt))
            {
                lastInteractedAt = state.LastInteractedAt;
            }
        }

        var allCompleted = completedCount == episodes.Count;
        var progressPercentage = allCompleted ? 100 : totalProgress / episodes.Count;

        if (!allCompleted && progressPercentage <= 0)
            return null;

        return new UserMediaStateDto
        {
            IsCompleted = allCompleted,
            ProgressPercentage = progressPercentage,
            LastPlaybackPosition = 0,
            PlayCount = 0,
            LastInteractedAt = lastInteractedAt
        };
    }
}
