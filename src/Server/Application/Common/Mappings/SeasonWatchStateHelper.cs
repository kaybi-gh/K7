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

        return AggregateFromEpisodeStates(
            episodes.Select(e => e.UserMediaStates.FirstOrDefault()).ToList());
    }

    public static UserMediaStateDto? AggregateFromEpisodeStates(IReadOnlyList<UserMediaState?> states)
    {
        if (states.Count == 0)
            return null;

        var completedCount = 0;
        var totalProgress = 0.0;
        DateTime? lastInteractedAt = null;

        foreach (var state in states)
        {
            if (state?.IsCompleted == true)
            {
                completedCount++;
                totalProgress += 100;
            }
            else
            {
                totalProgress += 0;
            }

            if (state?.LastInteractedAt is not null
                && (lastInteractedAt is null || state.LastInteractedAt > lastInteractedAt))
            {
                lastInteractedAt = state.LastInteractedAt;
            }
        }

        var allCompleted = completedCount == states.Count;
        var progressPercentage = allCompleted ? 100 : totalProgress / states.Count;

        if (!allCompleted && progressPercentage <= 0)
            return null;

        return new UserMediaStateDto
        {
            IsCompleted = allCompleted,
            ProgressPercentage = progressPercentage,
            LastPlaybackPosition = 0,
            PlayCount = 0,
            SkipCount = 0,
            LastInteractedAt = lastInteractedAt
        };
    }
}
