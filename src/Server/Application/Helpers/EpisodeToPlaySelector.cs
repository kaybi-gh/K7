using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Helpers;

public sealed record EpisodeToPlayCandidate(Guid Id, int SeasonNumber, int EpisodeNumber, UserMediaStateDto? State);

/// <summary>
/// Picks the episode a series page offers to play: the most recently touched episode in
/// progress, otherwise the first unwatched one, otherwise the first episode (rewatch).
/// Specials (season 0) come after the regular seasons.
/// </summary>
public static class EpisodeToPlaySelector
{
    public static Guid? Select(IEnumerable<EpisodeToPlayCandidate> candidates)
    {
        var ordered = candidates
            .OrderBy(c => c.SeasonNumber == 0 ? int.MaxValue : c.SeasonNumber)
            .ThenBy(c => c.EpisodeNumber)
            .ToList();

        if (ordered.Count == 0)
            return null;

        var inProgress = ordered
            .Where(IsInProgress)
            .OrderByDescending(c => c.State?.LastInteractedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (inProgress is not null)
            return inProgress.Id;

        return (ordered.FirstOrDefault(c => c.State?.IsCompleted != true) ?? ordered[0]).Id;
    }

    private static bool IsInProgress(EpisodeToPlayCandidate candidate) =>
        candidate.State is { IsCompleted: false } state
        && (state.LastPlaybackPosition >= 1 || state.ProgressPercentage is >= 1 and < 100);
}
