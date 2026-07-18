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

        var recommendations = await context.MediaRecommendations
            .AsNoTracking()
            .Where(r => seedMediaIds.Contains(r.MediaId))
            .ToListAsync(cancellationToken);

        if (recommendations.Count == 0)
            return [];

        var scoreByExternalKey = new Dictionary<(string Provider, string ExternalId), int>();
        for (var i = 0; i < seedMediaIds.Count; i++)
        {
            var seedId = seedMediaIds[i];
            var seedWeight = seedMediaIds.Count - i;
            var rec = recommendations.FirstOrDefault(r => r.MediaId == seedId);
            if (rec is null)
                continue;

            foreach (var externalId in rec.RecommendedIds)
            {
                var key = (rec.ProviderName, externalId);
                scoreByExternalKey[key] = scoreByExternalKey.GetValueOrDefault(key) + seedWeight;
            }
        }

        if (scoreByExternalKey.Count == 0)
            return [];

        var externalIdValues = scoreByExternalKey.Keys.Select(k => k.ExternalId).Distinct().ToList();
        var restrictionProfile = await mediaAccessFilter.GetRestrictionProfileAsync(userId, cancellationToken);
        var scoredCandidates = new Dictionary<Guid, (int Score, DateTimeOffset Created)>();
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
                    m.Created
                })
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var score = candidate.ExternalIds
                    .Sum(e => scoreByExternalKey.GetValueOrDefault((e.ProviderName, e.Value)));
                if (score > 0)
                    scoredCandidates.TryAdd(candidate.Id, (score, candidate.Created));
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
