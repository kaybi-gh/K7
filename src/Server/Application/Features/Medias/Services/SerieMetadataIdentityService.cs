using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Services;

public sealed record SerieIdentityMatch(
    string NumberingProviderName,
    string NumberingExternalId,
    IReadOnlyList<(string ProviderName, string ExternalId)> ExternalIds,
    int IdentityScore);

/// <summary>
/// Cross-provider serie identification: path ids, scored tournament, Auto canon via S/E hit-rate.
/// </summary>
public class SerieMetadataIdentityService(
    IEnumerable<ISearchableMetadataProvider> searchableProviders,
    IServiceProvider serviceProvider,
    ILogger<SerieMetadataIdentityService> logger)
{
    private readonly IReadOnlyList<ISearchableMetadataProvider> _searchableProviders = searchableProviders.ToList();
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<SerieMetadataIdentityService> _logger = logger;

    public async Task<SerieIdentityMatch?> ResolveAsync(
        MediaIdentification identification,
        string? libraryProviderName,
        IReadOnlyList<MediaIdentification> fileIdentifications,
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken = default)
    {
        var libraryMode = MetadataProviderHostMapper.NormalizeProviderName(libraryProviderName);

        if (!string.IsNullOrWhiteSpace(identification.ProviderName)
            && !string.IsNullOrWhiteSpace(identification.ProviderExternalId))
        {
            return await ResolveFromPathIdAsync(
                identification.ProviderName!,
                identification.ProviderExternalId!,
                libraryMode,
                fileIdentifications,
                language,
                fallbackLanguage,
                cancellationToken);
        }

        var searchProviders = SerieMetadataProviderCascade.ResolveSearchProviders(libraryMode);
        var query = identification.SeriesTitle ?? identification.Title;
        var year = identification.ReleaseYear?.Year;

        var candidateTasks = searchProviders.Select(async providerKey =>
        {
            ISearchableMetadataProvider? provider = null;
            try
            {
                var serieProvider = _serviceProvider.GetKeyedService<ISerieMetadataProvider>(providerKey);
                provider = serieProvider as ISearchableMetadataProvider;
            }
            catch
            {
                // Keyed resolution can throw for unknown keys; fall through.
            }

            provider ??= _searchableProviders.FirstOrDefault(p =>
                string.Equals(
                    MetadataProviderNames.Normalize(MetadataProviderHostMapper.NormalizeProviderName(p.ProviderName)),
                    MetadataProviderNames.Normalize(providerKey),
                    StringComparison.OrdinalIgnoreCase)
                && p is ISerieMetadataProvider);

            try
            {
                List<MetadataSearchResult> results = [];
                if (provider is not null)
                {
                    results = (await provider.SearchMetadataAsync(
                        query,
                        year,
                        providerId: null,
                        MediaType.Serie,
                        language ?? "en",
                        fallbackLanguage,
                        cancellationToken)).ToList();

                    if (results.Count == 0 && year.HasValue)
                    {
                        results = (await provider.SearchMetadataAsync(
                            query,
                            year: null,
                            providerId: null,
                            MediaType.Serie,
                            language ?? "en",
                            fallbackLanguage,
                            cancellationToken)).ToList();
                    }
                }

                if (results.Count == 0)
                {
                    var serieProvider = _serviceProvider.GetKeyedService<ISerieMetadataProvider>(providerKey);
                    if (serieProvider is not null)
                    {
                        var id = await serieProvider.SearchSerieAsync(
                            identification,
                            language,
                            fallbackLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var score = MetadataTitleMatchHelper.Score(query, year, query, year, popularity: null);
                            return
                            [
                                new ScoredCandidate(
                                    MetadataProviderNames.Normalize(providerKey),
                                    id,
                                    query,
                                    year,
                                    Popularity: null,
                                    score)
                            ];
                        }
                    }

                    return Array.Empty<ScoredCandidate>();
                }

                return results
                    .Select(r =>
                    {
                        var score = MetadataTitleMatchHelper.Score(
                            query,
                            year,
                            r.Title,
                            r.Year,
                            r.Popularity);
                        return new ScoredCandidate(
                            MetadataProviderNames.Normalize(
                                MetadataProviderHostMapper.NormalizeProviderName(r.Provider)),
                            r.ExternalId,
                            r.Title,
                            r.Year,
                            r.Popularity,
                            score);
                    })
                    .Where(c => !string.IsNullOrWhiteSpace(c.ExternalId))
                    .OrderByDescending(c => c.Score)
                    .Take(5)
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Serie search failed for provider {Provider}", providerKey);
                return Array.Empty<ScoredCandidate>();
            }
        });

        var allCandidates = (await Task.WhenAll(candidateTasks))
            .SelectMany(x => x)
            .ToList();

        if (allCandidates.Count == 0)
            return null;

        // Forced mode: prefer that provider's best candidate, fallback to global best.
        IEnumerable<ScoredCandidate> ranked = allCandidates.OrderByDescending(c => c.Score);
        if (!SerieMetadataProviderCascade.IsAuto(libraryMode)
            && libraryMode is MetadataProviderNames.Tmdb or MetadataProviderNames.Tvdb)
        {
            ranked = allCandidates
                .OrderByDescending(c => string.Equals(c.ProviderName, libraryMode, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.Score);
        }

        var best = PickBestWithYearGuard(ranked, year);
        if (best is null)
            return null;

        var externalIds = await BuildCrosswalkIdsAsync(
            best.ProviderName,
            best.ExternalId,
            language,
            fallbackLanguage,
            cancellationToken);

        var numberingProvider = best.ProviderName;
        var numberingExternalId = best.ExternalId;

        if (SerieMetadataProviderCascade.IsAuto(libraryMode))
        {
            var canon = await SelectCanonByHitRateAsync(
                externalIds,
                fileIdentifications,
                best.ProviderName,
                best.Score,
                cancellationToken);
            if (canon is not null)
            {
                numberingProvider = canon.Value.ProviderName;
                numberingExternalId = canon.Value.ExternalId;
            }
        }

        return new SerieIdentityMatch(
            numberingProvider,
            numberingExternalId,
            externalIds,
            best.Score);
    }

    private async Task<SerieIdentityMatch?> ResolveFromPathIdAsync(
        string providerName,
        string externalId,
        string libraryMode,
        IReadOnlyList<MediaIdentification> fileIdentifications,
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        var normalized = providerName.Equals(MetadataProviderNames.Imdb, StringComparison.OrdinalIgnoreCase)
            ? MetadataProviderNames.Imdb
            : MetadataProviderNames.Normalize(MetadataProviderHostMapper.NormalizeProviderName(providerName));

        var resolvedProvider = normalized == MetadataProviderNames.Imdb
            ? MetadataProviderNames.Tmdb
            : normalized;

        var externalIds = await BuildCrosswalkIdsAsync(
            resolvedProvider,
            externalId,
            language,
            fallbackLanguage,
            cancellationToken);

        if (externalIds.Count == 0)
            externalIds = [(resolvedProvider, externalId)];

        var numberingProvider = resolvedProvider;
        var numberingExternalId = externalIds
            .FirstOrDefault(e => string.Equals(e.ProviderName, resolvedProvider, StringComparison.OrdinalIgnoreCase))
            .ExternalId ?? externalId;

        if (SerieMetadataProviderCascade.IsAuto(libraryMode) && externalIds.Count > 1)
        {
            var canon = await SelectCanonByHitRateAsync(
                externalIds,
                fileIdentifications,
                numberingProvider,
                identityScore: 10_000,
                cancellationToken);
            if (canon is not null)
            {
                numberingProvider = canon.Value.ProviderName;
                numberingExternalId = canon.Value.ExternalId;
            }
        }
        else if (!SerieMetadataProviderCascade.IsAuto(libraryMode)
                 && libraryMode is MetadataProviderNames.Tmdb or MetadataProviderNames.Tvdb)
        {
            var forced = externalIds.FirstOrDefault(e =>
                string.Equals(e.ProviderName, libraryMode, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(forced.ExternalId))
            {
                numberingProvider = forced.ProviderName;
                numberingExternalId = forced.ExternalId;
            }
        }

        return new SerieIdentityMatch(numberingProvider, numberingExternalId, externalIds, 10_000);
    }

    private static ScoredCandidate? PickBestWithYearGuard(IEnumerable<ScoredCandidate> ranked, int? queryYear)
    {
        var list = ranked.ToList();
        if (list.Count == 0)
            return null;

        if (!queryYear.HasValue)
            return list[0];

        foreach (var candidate in list)
        {
            if (!candidate.Year.HasValue || candidate.Year.Value == queryYear.Value)
                return candidate;
        }

        // All mismatch year: still take best score (penalty already applied) rather than nothing.
        return list[0];
    }

    private async Task<IReadOnlyList<(string ProviderName, string ExternalId)>> BuildCrosswalkIdsAsync(
        string providerName,
        string externalId,
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        var ids = new List<(string ProviderName, string ExternalId)>();
        var normalized = MetadataProviderNames.Normalize(
            MetadataProviderHostMapper.NormalizeProviderName(providerName));

        try
        {
            var provider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(normalized);
            var metadata = await provider.FetchSerieMetadataAsync(
                externalId,
                language ?? "en",
                cancellationToken,
                fallbackLanguage);

            foreach (var external in metadata.ExternalIds)
            {
                if (string.IsNullOrWhiteSpace(external.ProviderName) || string.IsNullOrWhiteSpace(external.Value))
                    continue;

                var name = MetadataProviderNames.Normalize(
                    MetadataProviderHostMapper.NormalizeProviderName(external.ProviderName));
                if (name is not (MetadataProviderNames.Tmdb or MetadataProviderNames.Tvdb or MetadataProviderNames.Imdb))
                {
                    if (string.Equals(external.ProviderName, MetadataProviderNames.Imdb, StringComparison.OrdinalIgnoreCase))
                        name = MetadataProviderNames.Imdb;
                    else
                        continue;
                }

                if (ids.Any(i => string.Equals(i.ProviderName, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                ids.Add((name == MetadataProviderNames.Imdb ? MetadataProviderNames.Imdb : name, external.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Crosswalk fetch failed for {Provider}/{ExternalId}", providerName, externalId);
        }

        if (!ids.Any(i => string.Equals(i.ProviderName, normalized, StringComparison.OrdinalIgnoreCase)))
            ids.Insert(0, (normalized, externalId));

        return ids;
    }

    private async Task<(string ProviderName, string ExternalId)?> SelectCanonByHitRateAsync(
        IReadOnlyList<(string ProviderName, string ExternalId)> externalIds,
        IReadOnlyList<MediaIdentification> fileIdentifications,
        string identityWinnerProvider,
        int identityScore,
        CancellationToken cancellationToken)
    {
        var keys = fileIdentifications
            .Where(f => f.SeasonNumber.HasValue && f.EpisodeNumber.HasValue)
            .Select(f => (Season: f.SeasonNumber!.Value, Episode: f.EpisodeNumber!.Value))
            .Distinct()
            .ToList();

        if (keys.Count == 0)
            return null;

        var candidates = externalIds
            .Where(e => e.ProviderName is MetadataProviderNames.Tmdb or MetadataProviderNames.Tvdb)
            .GroupBy(e => e.ProviderName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
            return null;

        var scored = new List<(string ProviderName, string ExternalId, double HitRate, int IdentityBoost)>();
        foreach (var candidate in candidates)
        {
            try
            {
                var provider = _serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(candidate.ProviderName);
                var grid = await provider.ListEpisodeKeysAsync(candidate.ExternalId, cancellationToken);
                var hits = keys.Count(k => grid.Contains(k));
                var hitRate = (double)hits / keys.Count;
                var boost = string.Equals(candidate.ProviderName, identityWinnerProvider, StringComparison.OrdinalIgnoreCase)
                    ? identityScore
                    : 0;
                scored.Add((candidate.ProviderName, candidate.ExternalId, hitRate, boost));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Episode grid failed for {Provider}/{Id}", candidate.ProviderName, candidate.ExternalId);
            }
        }

        if (scored.Count == 0)
            return null;

        var best = scored
            .OrderByDescending(s => s.HitRate)
            .ThenByDescending(s => s.IdentityBoost)
            .ThenBy(s => s.ProviderName == MetadataProviderNames.Tmdb ? 0 : 1)
            .First();

        return (best.ProviderName, best.ExternalId);
    }

    private sealed record ScoredCandidate(
        string ProviderName,
        string ExternalId,
        string? Title,
        int? Year,
        double? Popularity,
        int Score);
}
