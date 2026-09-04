using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Mappings;

/// <summary>
/// Series and season cards store watch state on episodes. Aggregate those episode
/// states onto the parent so Watched/Progress stay accurate in list projections.
/// </summary>
internal static class SerieWatchStateAggregator
{
    private sealed record EpisodeRow(Guid Id, Guid SerieId, Guid SeasonId);

    public static async Task<IReadOnlyDictionary<Guid, UserMediaStateDto>> AggregateAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> serieIds,
        IReadOnlyCollection<Guid> seasonIds,
        Guid userId,
        Guid? sharedProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var serieIdSet = serieIds.ToHashSet();
        var seasonIdSet = seasonIds.ToHashSet();
        if (serieIdSet.Count == 0 && seasonIdSet.Count == 0)
            return new Dictionary<Guid, UserMediaStateDto>();

        var episodeRows = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Where(e => serieIdSet.Contains(e.SerieId) || seasonIdSet.Contains(e.SeasonId))
            .Select(e => new EpisodeRow(e.Id, e.SerieId, e.SeasonId))
            .ToListAsync(cancellationToken);

        if (episodeRows.Count == 0)
            return new Dictionary<Guid, UserMediaStateDto>();

        var episodeIds = episodeRows.Select(e => e.Id).ToList();
        Dictionary<Guid, UserMediaState?> statesByMediaId;

        if (sharedProfileId is { } profileId)
        {
            var sharedStates = await context.SharedProfileMediaStates
                .AsNoTracking()
                .Where(s => s.SharedProfileId == profileId && episodeIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            statesByMediaId = episodeIds.ToDictionary(
                id => id,
                id => sharedStates.TryGetValue(id, out var shared)
                    ? (UserMediaState?)shared.ToUserMediaState(userId)
                    : null);
        }
        else
        {
            var userStates = await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId && episodeIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            statesByMediaId = episodeIds.ToDictionary(
                id => id,
                id => userStates.TryGetValue(id, out var state) ? (UserMediaState?)state : null);
        }

        var result = new Dictionary<Guid, UserMediaStateDto>();

        AddAggregatedParents(
            result,
            episodeRows.Where(e => serieIdSet.Contains(e.SerieId)).GroupBy(e => e.SerieId),
            statesByMediaId);
        AddAggregatedParents(
            result,
            episodeRows.Where(e => seasonIdSet.Contains(e.SeasonId)).GroupBy(e => e.SeasonId),
            statesByMediaId);

        return result;
    }

    private static void AddAggregatedParents(
        Dictionary<Guid, UserMediaStateDto> result,
        IEnumerable<IGrouping<Guid, EpisodeRow>> groups,
        IReadOnlyDictionary<Guid, UserMediaState?> statesByMediaId)
    {
        foreach (var group in groups)
        {
            var states = group
                .Select(row => statesByMediaId.GetValueOrDefault(row.Id))
                .ToList();
            var aggregated = SeasonWatchStateHelper.AggregateFromEpisodeStates(states);
            if (aggregated is not null)
                result[group.Key] = aggregated;
        }
    }
}
