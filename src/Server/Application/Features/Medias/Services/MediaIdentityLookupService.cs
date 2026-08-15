using System.Linq.Expressions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Services;

/// <summary>
/// Shared lookup helpers for resolving existing media identity (by external id or normalized title key),
/// used by media creation flows to dedupe against already-indexed media.
/// </summary>
public class MediaIdentityLookupService(
    IApplicationDbContext context,
    IServiceProvider? serviceProvider = null,
    ILogger<MediaIdentityLookupService>? logger = null)
{
    private static readonly string[] SerieProviderKeys = ["tmdb", "tvdb"];
    private readonly Dictionary<string, Guid?> _providerSeriesCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Dictionary<(string Provider, string Value), (Guid MediaId, MediaType Type)>> LookupByExternalIdsAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<(string, string), (Guid MediaId, MediaType Type)>();
        var allPairs = items.SelectMany(i => i.ExternalIds.Select(e => (e.Key, e.Value))).Distinct().ToList();

        foreach (var batch in allPairs.Chunk(500))
        {
            var parameter = Expression.Parameter(typeof(ExternalId), "e");
            Expression? predicate = null;

            foreach (var (provider, value) in batch)
            {
                var providerEqual = Expression.Equal(
                    Expression.Call(
                        Expression.Property(parameter, nameof(ExternalId.ProviderName)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
                    Expression.Constant(provider.ToLowerInvariant()));
                var valueEqual = Expression.Equal(
                    Expression.Property(parameter, nameof(ExternalId.Value)),
                    Expression.Constant(value));
                var pair = Expression.AndAlso(providerEqual, valueEqual);
                predicate = predicate is null ? pair : Expression.OrElse(predicate, pair);
            }

            var mediaIdNotNull = Expression.NotEqual(
                Expression.Property(parameter, nameof(ExternalId.MediaId)),
                Expression.Constant(null, typeof(Guid?)));

            var fullPredicate = Expression.AndAlso(mediaIdNotNull, predicate!);
            var lambda = Expression.Lambda<Func<ExternalId, bool>>(fullPredicate, parameter);

            var matches = await context.ExternalIds
                .Where(lambda)
                .Select(e => new
                {
                    e.ProviderName,
                    e.Value,
                    e.MediaId,
                    Type = e.Media!.Type,
                    HasIndexedFiles = e.Media != null && e.Media.IndexedFiles.Any()
                })
                .ToListAsync(cancellationToken);

            foreach (var match in matches.Where(m => m.MediaId.HasValue)
                         .OrderByDescending(m => m.HasIndexedFiles))
            {
                result.TryAdd(
                    (match.ProviderName.ToLowerInvariant(), match.Value),
                    (match.MediaId!.Value, match.Type));
            }
        }

        return result;
    }

    public async Task<Dictionary<string, Guid>> LookupSeriesByTitleYearAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var titles = items
            .Select(i => i.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (titles.Count == 0) return result;

        var series = await context.Medias
            .OfType<Serie>()
            .Select(s => new SeriesYearRow(s.Id, s.Title, s.OriginalTitle, s.ReleaseDate))
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeSerieTitle(item.Title, item.Year);
            if (result.ContainsKey(key)) continue;

            var candidates = MediaIdentityKeys.ResolveSeriesMatches(
                item.Title,
                series,
                s => s.Title,
                s => s.OriginalTitle);

            var match = candidates.FirstOrDefault(s => MediaIdentityKeys.YearsCompatible(item.Year, s.ReleaseDate));
            if (match is null)
            {
                var providerId = await ResolveSeriesViaProvidersAsync(item.Title, item.Year, cancellationToken);
                if (providerId is not null)
                    match = series.FirstOrDefault(s => s.Id == providerId.Value);
            }

            if (match is not null)
                result.TryAdd(key, match.Id);
        }

        return result;
    }

    public async Task<Dictionary<string, Guid>> LookupMusicByTitleAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var resolvedItems = items.Select(item =>
        {
            var (title, artistName) = MediaIdentityKeys.ResolveMusicTitleAndArtist(item.Title, item.ArtistName);
            return (Item: item, Title: title, ArtistName: artistName);
        }).ToList();

        var allTitles = MediaIdentityKeys.TitleLookupVariants(
            resolvedItems.SelectMany(r => new[] { r.Item.Title, r.Title }).ToArray());

        var strippedTitles = allTitles
            .Select(MediaIdentityKeys.StripFeatureCredits)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        allTitles = MediaIdentityKeys.TitleLookupVariants([.. allTitles, .. strippedTitles]);

        if (allTitles.Count == 0) return result;

        var allTitlesLower = allTitles.Select(t => t.ToLowerInvariant()).ToList();
        var sortTitlesLower = allTitles
            .Select(MediaSortTitleHelper.Compute)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!.ToLowerInvariant())
            .Distinct()
            .ToList();

        // Exact title IN (...) keeps accented titles even when Postgres LOWER() is ASCII-only.
        // SortTitle is already folded (Efile), so it matches when LOWER(Title) does not.
        // Also include DB titles that only differ by (feat./ft./with ...) or a trailing " - Artist".
        var tracks = await context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.Title != null && (
                allTitles.Contains(t.Title)
                || (t.SortTitle != null && sortTitlesLower.Contains(t.SortTitle.ToLower()))
                || allTitlesLower.Contains(t.Title.ToLower())
                || allTitlesLower.Any(at =>
                    t.Title.ToLower().StartsWith(at + " (")
                    || t.Title.ToLower().StartsWith(at + " [")
                    || t.Title.ToLower().StartsWith(at + " - "))))
            .Select(t => new
            {
                t.Id,
                t.Title,
                AlbumTitle = t.Album != null ? t.Album.Title : null,
                ArtistName = t.Artist != null ? t.Artist.Title : (t.Album != null ? t.Album.Artist!.Title : null),
                ArtistSortTitle = t.Artist != null
                    ? t.Artist.SortTitle
                    : (t.Album != null ? t.Album.Artist!.SortTitle : null),
                HasFiles = t.IndexedFiles.Any()
            })
            .ToListAsync(cancellationToken);

        foreach (var resolved in resolvedItems)
        {
            var item = resolved.Item;
            var key = MediaIdentityKeys.NormalizeMusicTitle(item.ArtistName, item.Title);
            if (result.ContainsKey(key)) continue;

            var itemTitleCore = resolved.Title;
            var itemArtist = MediaIdentityKeys.IsVariousArtist(resolved.ArtistName)
                ? null
                : MediaIdentityKeys.NormalizePersonName(resolved.ArtistName);
            var itemAlbum = MediaIdentityKeys.StripAlbumEditionSuffix(
                MediaIdentityKeys.NormalizePersonName(item.AlbumName));

            // Title core must match on both sides (so "When You Know - Puggy" == "When You Know"
            // after stripping), then require artist and/or album - never title alone when artist is known.
            var candidates = tracks.Where(t =>
            {
                var dbTitleCore = MediaIdentityKeys.StripTrackEditionSuffix(
                    MediaIdentityKeys.StripRedundantArtistFromTitle(
                        MediaIdentityKeys.StripFeatureCredits(t.Title!), t.ArtistName));
                return MediaIdentityKeys.MatchesIgnoringDiacritics(t.Title, item.Title)
                    || MediaIdentityKeys.MatchesIgnoringDiacritics(t.Title, itemTitleCore)
                    || MediaIdentityKeys.MatchesIgnoringDiacritics(dbTitleCore, itemTitleCore);
            }).ToList();

            if (candidates.Count == 0)
                continue;

            var match = candidates
                .OrderByDescending(t => t.HasFiles)
                .FirstOrDefault(t =>
                {
                    var dbArtist = MediaIdentityKeys.NormalizePersonName(t.ArtistName);
                    var dbArtistSort = MediaIdentityKeys.NormalizePersonName(t.ArtistSortTitle);
                    var dbAlbum = MediaIdentityKeys.StripAlbumEditionSuffix(
                        MediaIdentityKeys.NormalizePersonName(t.AlbumTitle));
                    var artistMatch = itemArtist is not null
                        && (MediaIdentityKeys.PersonNamesMatch(dbArtist, itemArtist)
                            || MediaIdentityKeys.PersonNamesMatch(dbArtistSort, itemArtist));
                    var albumMatch = MediaIdentityKeys.AlbumTitlesOverlap(itemAlbum, dbAlbum)
                        || MediaIdentityKeys.AlbumTitlesOverlap(itemArtist, dbAlbum)
                        || MediaIdentityKeys.AlbumTitlesOverlap(itemAlbum, dbArtist);
                    return artistMatch || albumMatch;
                });

            // Only fall back to unique title when the source has no artist/album to compare.
            if (match is null && itemArtist is null && itemAlbum is null && candidates.Count == 1)
                match = candidates[0];

            if (match is not null)
                result.TryAdd(key, match.Id);
        }

        return result;
    }

    public async Task<Dictionary<string, Guid>> LookupMoviesByTitleYearAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var titles = MediaIdentityKeys.TitleLookupVariants(
            items.SelectMany(i => new[] { i.Title, i.OriginalTitle }).ToArray());

        if (titles.Count == 0) return result;

        var titlesLower = titles.Select(t => t.ToLowerInvariant()).ToList();
        var sortTitlesLower = titles
            .Select(MediaSortTitleHelper.Compute)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!.ToLowerInvariant())
            .Distinct()
            .ToList();

        var movies = await context.Medias
            .OfType<Movie>()
            .Where(m => m.IndexedFiles.Any() && m.Title != null && (
                titles.Contains(m.Title)
                || (m.OriginalTitle != null && titles.Contains(m.OriginalTitle))
                || (m.SortTitle != null && sortTitlesLower.Contains(m.SortTitle.ToLower()))
                || titlesLower.Contains(m.Title.ToLower())
                || (m.OriginalTitle != null && titlesLower.Contains(m.OriginalTitle.ToLower()))))
            .Select(m => new { m.Id, m.Title, m.OriginalTitle, m.ReleaseDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeMovieTitle(item.Title, item.Year);
            if (result.ContainsKey(key)) continue;

            var match = movies.FirstOrDefault(m =>
            {
                var titleMatch = MediaIdentityKeys.MatchesIgnoringDiacritics(m.Title, item.Title)
                    || MediaIdentityKeys.MatchesIgnoringDiacritics(m.OriginalTitle, item.Title)
                    || MediaIdentityKeys.MatchesIgnoringDiacritics(m.Title, item.OriginalTitle)
                    || MediaIdentityKeys.MatchesIgnoringDiacritics(m.OriginalTitle, item.OriginalTitle);
                return titleMatch && MediaIdentityKeys.YearsCompatible(item.Year, m.ReleaseDate);
            });

            if (match is not null)
                result.TryAdd(key, match.Id);
        }

        return result;
    }

    public async Task<Dictionary<string, Guid>> LookupEpisodesByIdentityAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var queryTitles = items
            .Select(i => i.SeriesTitle ?? "Unknown Series")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seriesRows = await context.Medias
            .OfType<Serie>()
            .Select(s => new SeriesRow(s.Id, s.Title, s.OriginalTitle))
            .ToListAsync(cancellationToken);

        var matchingSeriesIds = await LookupSeriesIdsByExternalIdsAsync(
            items.Select(i => i.SeriesExternalIds), cancellationToken);

        foreach (var queryTitle in queryTitles)
        {
            foreach (var id in await ResolveEpisodeSeriesIdsAsync(queryTitle, seriesRows, cancellationToken))
            {
                matchingSeriesIds.Add(id);
            }
        }

        if (matchingSeriesIds.Count == 0) return result;

        var episodes = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => matchingSeriesIds.Contains(e.SerieId))
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.OriginalTitle,
                e.EpisodeNumber,
                e.SerieId,
                HasIndexedFiles = e.IndexedFiles.Any(),
                SeasonNumber = e.Season != null ? e.Season.SeasonNumber : (int?)null
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeEpisodeKey(item.SeriesTitle, item.SeasonNumber, item.EpisodeNumber, item.Title);
            if (result.ContainsKey(key)) continue;

            var seriesTitle = item.SeriesTitle ?? "Unknown Series";
            var seasonNumber = item.SeasonNumber;
            var episodeNumber = item.EpisodeNumber;
            if ((!seasonNumber.HasValue || !episodeNumber.HasValue)
                && MediaIdentityKeys.TryParseSeasonEpisodeRange(item.Title, out var parsedSeason, out var parsedEpisode, out _))
            {
                seasonNumber ??= parsedSeason;
                episodeNumber ??= parsedEpisode;
            }

            var allowedSeriesIds = (await ResolveEpisodeSeriesIdsAsync(seriesTitle, seriesRows, cancellationToken))
                .ToHashSet();

            // Parent-series guids (Tautulli) often point at the franchise show. Use them
            // only when the episode title did not already pick a unique series.
            if (allowedSeriesIds.Count != 1)
            {
                foreach (var id in await LookupSeriesIdsByExternalIdsAsync([item.SeriesExternalIds], cancellationToken))
                    allowedSeriesIds.Add(id);
            }

            var seriesEpisodes = episodes
                .Where(e => allowedSeriesIds.Contains(e.SerieId))
                .ToList();

            var numbered = seriesEpisodes
                .Where(e =>
                {
                    if (seasonNumber.HasValue && e.SeasonNumber.HasValue && e.SeasonNumber != seasonNumber)
                        return false;

                    if (episodeNumber.HasValue && e.EpisodeNumber != episodeNumber)
                        return false;

                    if (!episodeNumber.HasValue
                        && !EpisodeTitleMatches(e.Title, e.OriginalTitle, item.Title, item.OriginalTitle))
                        return false;

                    return true;
                })
                .ToList();

            var match = numbered.Select(e => e.SerieId).Distinct().Count() == 1
                ? numbered.OrderByDescending(e => e.HasIndexedFiles).First()
                : null;

            // Anime often uses a different season layout than Plex; unique episode title still maps.
            if (match is null && !string.IsNullOrWhiteSpace(item.Title))
            {
                var byTitle = seriesEpisodes
                    .Where(e => EpisodeTitleMatches(e.Title, e.OriginalTitle, item.Title, item.OriginalTitle))
                    .ToList();
                if (byTitle.Count == 1)
                    match = byTitle[0];
            }

            if (match is not null)
                result.TryAdd(key, match.Id);
        }

        return result;
    }

    /// <summary>
    /// Finds a media of the given type by provider external id. Mirrors the single-item lookup pattern
    /// used by CreateMedia's FindOrCreate* helpers (e.g. series-by-external-id).
    /// </summary>
    public async Task<TMedia?> FindMediaByExternalIdAsync<TMedia>(
        string providerName,
        string value,
        CancellationToken cancellationToken = default) where TMedia : BaseMedia
    {
        var externalId = await context.ExternalIds
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Value == value
                && x.ProviderName == providerName
                && x.Media is TMedia, cancellationToken);

        return externalId?.Media as TMedia;
    }

    /// <summary>
    /// Finds a serie by title and calendar year. Same-title shows from different years stay distinct.
    /// Matches any ReleaseDate in that year (not exact DateOnly), so a parsed 2022-01-01 still hits
    /// a serie whose metadata refresh set the real premiere date.
    /// </summary>
    public Task<Serie?> FindSerieByTitleAndYearAsync(
        string title,
        DateOnly? releaseYear,
        CancellationToken cancellationToken = default)
    {
        if (releaseYear is null)
        {
            return context.Medias.OfType<Serie>()
                .Include(s => s.ExternalIds)
                .FirstOrDefaultAsync(s => s.Title == title && s.ReleaseDate == null, cancellationToken);
        }

        var yearStart = new DateOnly(releaseYear.Value.Year, 1, 1);
        var yearEnd = new DateOnly(releaseYear.Value.Year, 12, 31);

        return context.Medias.OfType<Serie>()
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync(
                s => s.Title == title
                    && s.ReleaseDate != null
                    && s.ReleaseDate >= yearStart
                    && s.ReleaseDate <= yearEnd,
                cancellationToken);
    }

    public async Task<MusicArtist?> FindMusicArtistByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = MusicArtistNameNormalizer.NormalizeForMatch(name);
        var candidates = await context.Medias.OfType<MusicArtist>()
            .Where(a => a.Title == name
                || (normalized != null && a.Title == normalized))
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(a => MusicArtistNameNormalizer.NamesMatch(a.Title, name));
    }

    private async Task<HashSet<Guid>> ResolveEpisodeSeriesIdsAsync(
        string queryTitle,
        IReadOnlyList<SeriesRow> seriesRows,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var match in MediaIdentityKeys.ResolveSeriesMatches(
            queryTitle, seriesRows, s => s.Title, s => s.OriginalTitle))
        {
            ids.Add(match.Id);
        }

        // A unique full-title hit is the show. Do not also add the franchise parent
        // (Ranking of Kings vs Ranking of Kings : Le tresor...) or S01Exx is ambiguous.
        if (ids.Count == 1)
            return ids;

        // Only when the full title did not resolve: two+ short-name hits (Konosuba +
        // spin-off) stay in play and SxxExx picks. Do not add them on top of an exact
        // title hit, or S01Exx on both shows becomes ambiguous.
        // A single short-name hit is ignored (DanMachi must not bind to Sword Oratoria).
        if (ids.Count == 0)
        {
            var prefix = MediaIdentityKeys.FindSeriesByShortNamePrefix(
                queryTitle, seriesRows, s => s.Title, s => s.OriginalTitle);
            if (prefix.Count >= 2)
            {
                foreach (var match in prefix)
                    ids.Add(match.Id);
            }
        }

        if (ids.Count == 0)
        {
            var token = MediaIdentityKeys.DistinctiveLastToken(queryTitle);
            var tokenHits = MediaIdentityKeys.FindSeriesContainingToken(
                token, seriesRows, s => s.Title, s => s.OriginalTitle);
            foreach (var match in tokenHits)
                ids.Add(match.Id);
        }

        if (ids.Count == 0)
        {
            var providerId = await ResolveSeriesViaProvidersAsync(queryTitle, year: null, cancellationToken);
            if (providerId is not null)
                ids.Add(providerId.Value);
        }

        return ids;
    }

    private sealed record SeriesRow(Guid Id, string? Title, string? OriginalTitle);

    private sealed record SeriesYearRow(Guid Id, string? Title, string? OriginalTitle, DateOnly? ReleaseDate);

    private async Task<HashSet<Guid>> LookupSeriesIdsByExternalIdsAsync(
        IEnumerable<Dictionary<string, string>?> seriesIdSets,
        CancellationToken cancellationToken)
    {
        var fakeItems = seriesIdSets
            .Where(ids => ids is { Count: > 0 })
            .Select((ids, index) => new BulkCreateMediasRequest.BulkCreateMediaItem
            {
                Key = $"series-ext-{index}",
                MediaType = "serie",
                Title = "",
                ExternalIds = ids!
            })
            .ToList();

        if (fakeItems.Count == 0)
            return [];

        var lookup = await LookupByExternalIdsAsync(fakeItems, cancellationToken);
        return lookup.Values
            .Where(hit => hit.Type == MediaType.Serie)
            .Select(hit => hit.MediaId)
            .ToHashSet();
    }

    private async Task<Guid?> ResolveSeriesViaProvidersAsync(
        string title,
        int? year,
        CancellationToken cancellationToken)
    {
        if (serviceProvider is null || string.IsNullOrWhiteSpace(title))
            return null;

        var cacheKey = year is null ? title : $"{title}|{year.Value}";
        if (_providerSeriesCache.TryGetValue(cacheKey, out var cached))
            return cached;

        Guid? resolved = null;
        var identification = new MediaIdentification(title)
        {
            SeriesTitle = title,
            ReleaseYear = year is > 0 ? new DateOnly(year.Value, 1, 1) : null
        };

        foreach (var providerKey in SerieProviderKeys)
        {
            var provider = serviceProvider.GetKeyedService<ISerieMetadataProvider>(providerKey);
            if (provider is null)
                continue;

            try
            {
                var providerId = await provider.SearchSerieAsync(
                    identification,
                    "fr",
                    MetadataProviderNames.DefaultLanguage,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(providerId))
                    continue;

                var serie = await FindMediaByExternalIdAsync<Serie>(
                    provider.ProviderName, providerId, cancellationToken);
                if (serie is null)
                    continue;

                logger?.LogInformation(
                    "Resolved series {Title} via {Provider} {ProviderId} to {MediaId}",
                    title, provider.ProviderName, providerId, serie.Id);
                resolved = serie.Id;
                break;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Series provider search failed for {Title} via {Provider}", title, providerKey);
            }
        }

        _providerSeriesCache[cacheKey] = resolved;
        return resolved;
    }

    private static bool EpisodeTitleMatches(string? dbTitle, string? dbOriginal, string? itemTitle, string? itemOriginal)
    {
        if (MediaIdentityKeys.MatchesIgnoringDiacritics(dbTitle, itemTitle)
            || MediaIdentityKeys.MatchesIgnoringDiacritics(dbOriginal, itemTitle)
            || MediaIdentityKeys.MatchesIgnoringDiacritics(dbTitle, itemOriginal)
            || MediaIdentityKeys.MatchesIgnoringDiacritics(dbOriginal, itemOriginal))
        {
            return true;
        }

        foreach (var left in MediaIdentityKeys.EpisodeTitleSegments(dbTitle, dbOriginal))
        {
            foreach (var right in MediaIdentityKeys.EpisodeTitleSegments(itemTitle, itemOriginal))
            {
                if (MediaIdentityKeys.MatchesIgnoringDiacritics(left, right))
                    return true;
            }
        }

        return false;
    }
}
