using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public class HomeRecommendationService(
    IApplicationDbContext context,
    MediaAccessFilter mediaAccessFilter) : IHomeRecommendationService
{
    private const int SeedSessionLimit = 20;
    private const int SeedStateLimit = 10;

    // Genre overlap only boosts candidates already surfaced by the provider-based recommendation
    // signal; it never introduces new candidates on its own.
    private const double GenreBoostFactor = 0.5;

    public async Task<IReadOnlyList<Guid>> GetRecommendedMediaIdsAsync(
        Guid userId,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var seedMediaIds = await GetSeedMediaIdsAsync(userId, cancellationToken);
        if (seedMediaIds.Count == 0)
            return [];

        return await GetRecommendationsForSeedsAsync(userId, seedMediaIds, libraryIds, pageNumber, pageSize, cancellationToken);
    }

    public async Task<string?> GetBecauseYouWatchedTitleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var seed = await GetBecauseYouWatchedSeedAsync(userId, cancellationToken);
        return seed?.Title;
    }

    public async Task<IReadOnlyList<Guid>> GetBecauseYouWatchedMediaIdsAsync(
        Guid userId,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var seed = await GetBecauseYouWatchedSeedAsync(userId, cancellationToken);
        if (seed is null)
            return [];

        return await GetRecommendationsForSeedsAsync(userId, [seed.Value.MediaId], libraryIds, pageNumber, pageSize, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> GetRecommendationsForSeedsAsync(
        Guid userId,
        IReadOnlyList<Guid> seedMediaIds,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (seedMediaIds.Count == 0)
            return [];

        var recommendations = await context.MediaRecommendations
            .AsNoTracking()
            .Where(r => seedMediaIds.Contains(r.MediaId))
            .ToListAsync(cancellationToken);

        if (recommendations.Count == 0)
            return [];

        var seedGenresByMediaId = await context.Medias
            .AsNoTracking()
            .Where(m => seedMediaIds.Contains(m.Id))
            .Select(m => new
            {
                m.Id,
                Genres = m.MetadataTags
                    .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                    .Select(mt => mt.MetadataTag.NormalizedKey)
                    .ToList()
            })
            .ToDictionaryAsync(x => x.Id, x => x.Genres, cancellationToken);

        var scoreByExternalKey = new Dictionary<(string Provider, string ExternalId), int>();
        var genreWeightByKey = new Dictionary<string, int>();
        for (var i = 0; i < seedMediaIds.Count; i++)
        {
            var seedId = seedMediaIds[i];
            var seedWeight = seedMediaIds.Count - i;

            var rec = recommendations.FirstOrDefault(r => r.MediaId == seedId);
            if (rec is not null)
            {
                foreach (var externalId in rec.RecommendedIds)
                {
                    var key = (rec.ProviderName, externalId);
                    scoreByExternalKey[key] = scoreByExternalKey.GetValueOrDefault(key) + seedWeight;
                }
            }

            if (seedGenresByMediaId.TryGetValue(seedId, out var genres))
            {
                foreach (var genre in genres)
                    genreWeightByKey[genre] = genreWeightByKey.GetValueOrDefault(genre) + seedWeight;
            }
        }

        if (scoreByExternalKey.Count == 0)
            return [];

        var externalIdValues = scoreByExternalKey.Keys.Select(k => k.ExternalId).Distinct().ToList();
        var restrictionProfile = await mediaAccessFilter.GetRestrictionProfileAsync(userId, cancellationToken);
        var scoredCandidates = new Dictionary<Guid, (double Score, DateTimeOffset Created)>();
        foreach (var externalIdBatch in externalIdValues.Chunk(500))
        {
            var query = context.Medias
                .AsNoTracking()
                .Where(m => !seedMediaIds.Contains(m.Id))
                .Where(m => m.ExternalIds.Any(e => externalIdBatch.Contains(e.Value)));

            if (libraryIds is { Length: > 0 })
                query = query.WhereAvailableInLibraries(context, libraryIds);

            query = mediaAccessFilter.ApplyExclusions(query, userId);
            if (restrictionProfile is not null)
                query = ContentRestrictionEvaluator.ApplyRestriction(query, restrictionProfile);

            var candidates = await query
                .Select(m => new
                {
                    m.Id,
                    ExternalIds = m.ExternalIds.Select(e => new { e.ProviderName, e.Value }).ToList(),
                    Genres = m.MetadataTags
                        .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                        .Select(mt => mt.MetadataTag.NormalizedKey)
                        .ToList(),
                    m.Created
                })
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var externalScore = candidate.ExternalIds
                    .Sum(e => scoreByExternalKey.GetValueOrDefault((e.ProviderName, e.Value)));
                if (externalScore <= 0)
                    continue;

                var genreScore = candidate.Genres.Sum(g => genreWeightByKey.GetValueOrDefault(g));
                var totalScore = externalScore + (GenreBoostFactor * genreScore);
                scoredCandidates.TryAdd(candidate.Id, (totalScore, candidate.Created));
            }
        }

        var ranked = scoredCandidates
            .Select(candidate => new { candidate.Key, candidate.Value.Score, candidate.Value.Created })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Created)
            .Select(x => x.Key)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return ranked;
    }

    private async Task<(Guid MediaId, string Title)?> GetBecauseYouWatchedSeedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var seedId = await context.MediaPlaybackSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.CompletedAt != null && (s.Media is Movie || s.Media is SerieEpisode))
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => s.MediaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (seedId == Guid.Empty)
        {
            seedId = await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.IsCompleted && (s.Media is Movie || s.Media is SerieEpisode))
                .OrderByDescending(s => s.LastInteractedAt)
                .Select(s => s.MediaId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (seedId == Guid.Empty)
            return null;

        var seedMedia = await context.Medias
            .AsNoTracking()
            .Include(m => ((SerieEpisode)m).Serie)
            .FirstOrDefaultAsync(m => m.Id == seedId, cancellationToken);

        var title = seedMedia switch
        {
            SerieEpisode episode => episode.Serie?.Title ?? episode.Title,
            not null => seedMedia.Title,
            null => null
        };

        return string.IsNullOrWhiteSpace(title) ? null : (seedId, title);
    }

    private async Task<List<Guid>> GetSeedMediaIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var fromSessions = await context.MediaPlaybackSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && (s.CompletedAt != null || s.StoppedAt != null))
            .OrderByDescending(s => s.CompletedAt ?? s.StoppedAt)
            .Take(SeedSessionLimit)
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (fromSessions.Count >= 5)
            return fromSessions;

        var fromStates = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.LastInteractedAt != null)
            .OrderByDescending(s => s.LastInteractedAt)
            .Take(SeedStateLimit)
            .Select(s => s.MediaId)
            .ToListAsync(cancellationToken);

        return [.. fromSessions.Concat(fromStates).Distinct()];
    }
}
