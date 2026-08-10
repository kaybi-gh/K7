using System.Linq.Expressions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Services;

/// <summary>
/// Shared lookup helpers for resolving existing media identity (by external id or normalized title key),
/// used by media creation flows to dedupe against already-indexed media.
/// </summary>
public class MediaIdentityLookupService(IApplicationDbContext context)
{
    public async Task<Dictionary<(string Provider, string Value), Guid>> LookupByExternalIdsAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<(string, string), Guid>();
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
                    HasIndexedFiles = e.Media != null && e.Media.IndexedFiles.Any()
                })
                .ToListAsync(cancellationToken);

            foreach (var match in matches.Where(m => m.MediaId.HasValue)
                         .OrderByDescending(m => m.HasIndexedFiles))
            {
                result.TryAdd((match.ProviderName.ToLowerInvariant(), match.Value), match.MediaId!.Value);
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

        var titlesLower = titles.Select(t => t.ToLowerInvariant()).ToList();

        var series = await context.Medias
            .OfType<Serie>()
            .Where(s => s.Title != null && titlesLower.Contains(s.Title.ToLower()))
            .Select(s => new { s.Id, s.Title, s.ReleaseDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeSerieTitle(item.Title, item.Year);
            if (result.ContainsKey(key)) continue;

            var match = series.FirstOrDefault(s =>
            {
                if (!string.Equals(s.Title, item.Title, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (item.Year is null || s.ReleaseDate is null)
                    return true;

                return s.ReleaseDate.Value.Year == item.Year.Value;
            });

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

        var trackTitles = items
            .Select(i => i.Title)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedTitles = items
            .Select(i => MediaIdentityKeys.StripRedundantArtistFromTitle(
                MediaIdentityKeys.StripFeatureCredits(i.Title), i.ArtistName))
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var strippedTitles = trackTitles
            .Select(MediaIdentityKeys.StripFeatureCredits)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allTitles = trackTitles.Concat(strippedTitles).Concat(normalizedTitles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allTitles.Count == 0) return result;

        var allTitlesLower = allTitles.Select(t => t.ToLowerInvariant()).ToList();

        // Include DB titles that only differ by (feat./ft./with ...) or a trailing " - Artist".
        var tracks = await context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.Title != null && t.IndexedFiles.Any())
            .Where(t => allTitlesLower.Any(at =>
                t.Title!.ToLower() == at
                || t.Title.ToLower().StartsWith(at + " (")
                || t.Title.ToLower().StartsWith(at + " [")
                || t.Title.ToLower().StartsWith(at + " - ")))
            .Select(t => new
            {
                t.Id,
                t.Title,
                AlbumTitle = t.Album != null ? t.Album.Title : null,
                ArtistName = t.Artist != null ? t.Artist.Title : (t.Album != null ? t.Album.Artist!.Title : null)
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeMusicTitle(item.ArtistName, item.Title);
            if (result.ContainsKey(key)) continue;

            var itemTitleCore = MediaIdentityKeys.StripRedundantArtistFromTitle(
                MediaIdentityKeys.StripFeatureCredits(item.Title), item.ArtistName);
            var itemArtist = MediaIdentityKeys.NormalizePersonName(item.ArtistName);
            var itemAlbum = MediaIdentityKeys.NormalizePersonName(item.AlbumName);

            // Title core must match on both sides (so "When You Know - Puggy" == "When You Know"
            // after stripping), then require artist and/or album - never title alone when artist is known.
            var candidates = tracks.Where(t =>
            {
                var dbTitleCore = MediaIdentityKeys.StripRedundantArtistFromTitle(
                    MediaIdentityKeys.StripFeatureCredits(t.Title!), t.ArtistName);
                return string.Equals(t.Title, item.Title, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dbTitleCore, itemTitleCore, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (candidates.Count == 0)
                continue;

            var match = candidates.FirstOrDefault(t =>
            {
                var dbArtist = MediaIdentityKeys.NormalizePersonName(t.ArtistName);
                var dbAlbum = MediaIdentityKeys.NormalizePersonName(t.AlbumTitle);
                var artistMatch = itemArtist is not null && dbArtist is not null
                    && string.Equals(dbArtist, itemArtist, StringComparison.OrdinalIgnoreCase);
                var albumMatch = itemAlbum is not null && dbAlbum is not null
                    && string.Equals(dbAlbum, itemAlbum, StringComparison.OrdinalIgnoreCase);
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

        var titles = items
            .Select(i => i.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (titles.Count == 0) return result;

        var titlesLower = titles.Select(t => t.ToLowerInvariant()).ToList();

        var movies = await context.Medias
            .OfType<Movie>()
            .Where(m => m.Title != null && titlesLower.Contains(m.Title.ToLower()))
            .Where(m => m.IndexedFiles.Any())
            .Select(m => new { m.Id, m.Title, m.ReleaseDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = MediaIdentityKeys.NormalizeMovieTitle(item.Title, item.Year);
            if (result.ContainsKey(key)) continue;

            var match = movies.FirstOrDefault(m =>
            {
                if (!string.Equals(m.Title, item.Title, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (item.Year is null || m.ReleaseDate is null)
                    return true;

                return m.ReleaseDate.Value.Year == item.Year.Value;
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

        var seriesTitles = items
            .Select(i => i.SeriesTitle ?? "Unknown Series")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (seriesTitles.Count == 0) return result;

        var episodes = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.Serie != null && e.Serie.Title != null && seriesTitles.Contains(e.Serie.Title))
            .Where(e => e.IndexedFiles.Any())
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.EpisodeNumber,
                SeriesTitle = e.Serie!.Title,
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

            var match = episodes.FirstOrDefault(e =>
            {
                if (!string.Equals(e.SeriesTitle, seriesTitle, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (seasonNumber.HasValue && e.SeasonNumber.HasValue && e.SeasonNumber != seasonNumber)
                    return false;

                if (episodeNumber.HasValue && e.EpisodeNumber != episodeNumber)
                    return false;

                if (!episodeNumber.HasValue && !string.Equals(e.Title, item.Title, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });

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
    /// Finds a serie by exact title and release date (including both null). Aligns with movie
    /// dedupe so same-title shows from different years (e.g. One Piece anime vs live-action) stay distinct.
    /// </summary>
    public Task<Serie?> FindSerieByTitleAndYearAsync(
        string title,
        DateOnly? releaseYear,
        CancellationToken cancellationToken = default) =>
        context.Medias.OfType<Serie>()
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync(s => s.Title == title && s.ReleaseDate == releaseYear, cancellationToken);

    public async Task<MusicArtist?> FindMusicArtistByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = MusicArtistNameNormalizer.NormalizeForMatch(name);
        var candidates = await context.Medias.OfType<MusicArtist>()
            .Where(a => a.Title == name
                || (normalized != null && a.Title == normalized))
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(a => MusicArtistNameNormalizer.NamesMatch(a.Title, name));
    }
}
