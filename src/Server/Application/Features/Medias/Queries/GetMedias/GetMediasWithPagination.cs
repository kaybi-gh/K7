using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Application.Features.Medias.Queries.GetMedias;

public record GetMediasWithPaginationQuery : IRequest<PaginatedList<BaseMedia>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid[]? Ids { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public bool? ContinueWatching { get; init; }
    public Guid[]? PersonIds { get; init; }
    public Guid[]? ArtistIds { get; init; }
    public string[]? Genres { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public MediaProvenance? Provenance { get; init; }
    public string? SearchText { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediasQueryHandler(IApplicationDbContext context, IUser currentUser, IMemoryCache cache, IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<BaseMedia>>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public async Task<PaginatedList<BaseMedia>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(request, currentUser.Id);
        var version = cacheInvalidator.Version;

        if (cache.TryGetValue(cacheKey, out (long Version, PaginatedList<BaseMedia> Result) cached) && cached.Version == version)
            return cached.Result;

        var result = await ExecuteQueryAsync(request, cancellationToken);
        cache.Set(cacheKey, (version, result), CacheDuration);
        return result;
    }

    private static string BuildCacheKey(GetMediasWithPaginationQuery request, Guid? userId)
    {
        var parts = new List<string> { "medias", $"u:{userId}" };

        if (request.LibraryIds is { Length: > 0 })
            parts.Add($"lib:{string.Join(',', request.LibraryIds.OrderBy(x => x))}");
        if (request.LibraryGroupIds is { Length: > 0 })
            parts.Add($"lg:{string.Join(',', request.LibraryGroupIds.OrderBy(x => x))}");
        if (request.Ids is { Length: > 0 })
            parts.Add($"ids:{string.Join(',', request.Ids.OrderBy(x => x))}");
        if (request.ContinueWatching.HasValue)
            parts.Add($"cw:{request.ContinueWatching.Value}");
        if (request.UnwatchedOnly.HasValue)
            parts.Add($"uw:{request.UnwatchedOnly.Value}");
        if (request.PersonIds is { Length: > 0 })
            parts.Add($"pid:{string.Join(',', request.PersonIds.OrderBy(x => x))}");
        if (request.Genres is { Length: > 0 })
            parts.Add($"g:{string.Join(',', request.Genres.Order())}");
        if (request.MediaTypes is { Count: > 0 })
            parts.Add($"mt:{string.Join(',', request.MediaTypes.Order())}");
        if (request.OrderBy is { Count: > 0 })
            parts.Add($"ob:{string.Join(',', request.OrderBy.Order())}");
        if (request.Provenance.HasValue)
            parts.Add($"prov:{request.Provenance.Value}");
        if (!string.IsNullOrWhiteSpace(request.SearchText))
            parts.Add($"st:{request.SearchText.Trim()}");
        parts.Add($"p:{request.PageNumber}");
        parts.Add($"ps:{request.PageSize}");

        return string.Join('|', parts);
    }

    private async Task<PaginatedList<BaseMedia>> ExecuteQueryAsync(GetMediasWithPaginationQuery request, CancellationToken cancellationToken)
    {
        Guid? userId = currentUser.Id;

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var filterQuery = context.Medias
            .AsNoTracking()
            .AsQueryable();

        filterQuery = ApplyFilters(request, filterQuery, userId);

        if (userId.HasValue)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            filterQuery = filterQuery.Where(x =>
                x is MusicAlbum
                    ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                    : x is MusicArtist
                        ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || a.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                    : x is MusicTrack
                        ? x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                    : x is Serie
                        ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                        : x is SerieSeason
                            ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                            : x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)));

            var excludedMediaIds = context.UserMediaExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.MediaId);

            filterQuery = filterQuery.WhereNotUserExcluded(excludedMediaIds);

            var restrictionProfile = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId.Value), cancellationToken);

            if (restrictionProfile is not null)
                filterQuery = ContentRestrictionEvaluator.ApplyRestriction(filterQuery, restrictionProfile);
        }

        var totalCount = await filterQuery.CountAsync(cancellationToken);

        var applicableProviders = await MediaOrderingHelper.ResolveApplicableProvidersAsync(
            context, request.LibraryIds, cancellationToken);
        var recentCutoff = DateTime.UtcNow.AddDays(-14);
        var providerRatings = context.Ratings.OfType<MetadataProviderRating>().AsNoTracking();

        var pageIds = await ApplyOrdering(request.OrderBy, filterQuery, userId, applicableProviders, recentCutoff, providerRatings)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
            return new PaginatedList<BaseMedia>([], totalCount, request.PageNumber, request.PageSize);

        var dataQuery = context.Medias
            .Where(m => pageIds.Contains(m.Id))
            .Include(x => x.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
            .Include(x => x.IndexedFiles)
            .AsNoTracking()
            .AsQueryable();

        if (request.MediaTypes?.Contains(MediaType.MusicTrack) == true
            || request.MediaTypes == null)
        {
            dataQuery = dataQuery
                .Include(x => ((MusicTrack)x).Album)
                    .ThenInclude(a => a.Artist)
                .Include(x => ((MusicTrack)x).Artist)
                .Include(x => ((MusicTrack)x).Album)
                    .ThenInclude(a => a.Pictures)
                        .ThenInclude(p => p.Variants)
                .Include(x => ((MusicTrack)x).AudioAnalysis);
        }

        if (request.MediaTypes?.Contains(MediaType.MusicAlbum) == true
            || request.MediaTypes == null)
        {
            dataQuery = dataQuery
                .Include(x => ((MusicAlbum)x).Artist);
        }

        if (request.MediaTypes?.Contains(MediaType.SerieEpisode) == true
            || request.ContinueWatching == true
            || request.MediaTypes == null)
        {
            dataQuery = dataQuery
                .Include(x => ((SerieEpisode)x).Season)
                    .ThenInclude(s => s.Pictures)
                        .ThenInclude(p => p.Variants)
                .Include(x => ((SerieEpisode)x).Serie)
                    .ThenInclude(s => s.Pictures)
                        .ThenInclude(p => p.Variants)
                .Include(x => ((SerieEpisode)x).Serie)
                    .ThenInclude(s => s.Seasons);
        }

        if (request.MediaTypes?.Contains(MediaType.SerieSeason) == true)
        {
            dataQuery = dataQuery.Include(x => ((SerieSeason)x).Episodes);
        }

        if (userId.HasValue)
        {
            dataQuery = dataQuery.Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        var items = await dataQuery.AsSplitQuery().ToListAsync(cancellationToken);
        var ordered = pageIds.Select(id => items.First(m => m.Id == id)).ToList();

        return new PaginatedList<BaseMedia>(ordered, totalCount, request.PageNumber, request.PageSize);
    }

    internal static IQueryable<BaseMedia> ApplyFilters(GetMediasWithPaginationQuery request, IQueryable<BaseMedia> query, Guid? userId)
    {
        var includeSeasons = request.MediaTypes?.Contains(MediaType.SerieSeason) == true;
        var includeEpisodes = request.MediaTypes?.Contains(MediaType.SerieEpisode) == true;
        query = query.Where(x => x is MusicAlbum || x is MusicArtist || x is MusicTrack || x is Serie || (includeSeasons && x is SerieSeason)
            || (includeEpisodes && x is SerieEpisode)
            || x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());

        if (request.LibraryIds?.Length > 0)
        {
            query = query.Where(x =>
                x is MusicAlbum
                    ? x.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                        || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId)))
                    : x is MusicArtist
                        ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                            || a.Tracks.Any(t => t.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                || t.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))))
                        : x is MusicTrack
                            ? x.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                || x.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                                || ((MusicTrack)x).Album.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                                || ((MusicTrack)x).Album.Tracks.Any(t => t.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId)))
                            : x is Serie
                                ? x.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                                    || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                        || e.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))))
                                : x is SerieSeason
                                    ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                        || e.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId)))
                                    : x is SerieEpisode
                                        ? x.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                            || x.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))
                                            || ((SerieEpisode)x).Serie.Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                                || e.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId))))
                                        : x.IndexedFiles.Any(f => request.LibraryIds.Contains(f.LibraryId))
                                            || x.RemoteIndexedFiles.Any(r => request.LibraryIds.Contains(r.LibraryId)));
        }

        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaTypes?.Count > 0)
        {
            query = query.Where(x => request.MediaTypes.Contains(x.Type));
        }

        if (request.ContinueWatching == true && userId.HasValue)
        {
            query = query.Where(x =>
                !(x is MusicAlbum) && !(x is MusicTrack)
                && x.UserMediaStates.Any(s =>
                    s.UserId == userId.Value
                    && !s.IsCompleted
                    && s.LastInteractedAt != null));
        }

        if (request.PersonIds?.Length > 0)
        {
            query = query.Where(x => x.PersonRoles.Any(r => request.PersonIds.Contains(r.PersonId))
                || (x is MusicTrack && ((MusicTrack)x).Album.PersonRoles.Any(r => request.PersonIds.Contains(r.PersonId))));
        }

        if (request.ArtistIds?.Length > 0)
        {
            query = query.Where(x =>
                (x is MusicTrack && (request.ArtistIds.Contains(((MusicTrack)x).ArtistId!.Value) || request.ArtistIds.Contains(((MusicTrack)x).Album!.ArtistId!.Value)))
                || (x is MusicAlbum && request.ArtistIds.Contains(((MusicAlbum)x).ArtistId!.Value)));
        }

        if (request.Genres?.Length > 0)
        {
            query = query.Where(x => x.Genres.Any(g => request.Genres.Contains(g))
                || (x is MusicTrack && ((MusicTrack)x).Album.Genres.Any(g => request.Genres.Contains(g))));
        }

        if (request.Provenance.HasValue)
        {
            query = request.Provenance.Value switch
            {
                MediaProvenance.Local => query.Where(x => x.IndexedFiles.Any()),
                MediaProvenance.Federation => query.Where(x => x.RemoteIndexedFiles.Any()),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var term = request.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title != null && EF.Functions.Like(x.Title.ToLower(), $"%{term}%"));
        }

        if (request.UnwatchedOnly == true && userId.HasValue)
        {
            query = query.Where(x => !x.UserMediaStates.Any(s => s.UserId == userId.Value && s.IsCompleted));
        }

        return query;
    }

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(
        HashSet<MediaOrderingOption>? orderBy,
        IQueryable<BaseMedia> queryable,
        Guid? userId,
        HashSet<MetadataProvider> applicableProviders,
        DateTime recentCutoff,
        IQueryable<MetadataProviderRating> providerRatings)
    {
        ArgumentException.ThrowIfNullOrEmpty(nameof(orderBy));
        IOrderedQueryable<BaseMedia>? orderedQueryable = null;

        if (orderBy == null || orderBy.Count == 0)
        {
            return queryable.OrderByDescending(x => x.Id);
        }

        foreach (var order in orderBy)
        {
            orderedQueryable = order switch
            {
                MediaOrderingOption.CreatedAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Id)
                    : orderedQueryable.ThenBy(x => x.Id),
                MediaOrderingOption.CreatedDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Id)
                    : orderedQueryable.ThenByDescending(x => x.Id),
                MediaOrderingOption.LastInteractedAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault())
                    : orderedQueryable.ThenBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault()),
                MediaOrderingOption.LastInteractedDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault())
                    : orderedQueryable.ThenByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.LastInteractedAt)
                        .FirstOrDefault()),
                MediaOrderingOption.LocalRatingAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Ratings
                        .OfType<UserRating>()
                        .Where(r => !userId.HasValue || r.UserId == userId.Value)
                        .Select(r => (double?)r.Value)
                        .FirstOrDefault())
                    : orderedQueryable.ThenBy(x => x.Ratings
                        .OfType<UserRating>()
                        .Where(r => !userId.HasValue || r.UserId == userId.Value)
                        .Select(r => (double?)r.Value)
                        .FirstOrDefault()),
                MediaOrderingOption.LocalRatingDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Ratings
                        .OfType<UserRating>()
                        .Where(r => !userId.HasValue || r.UserId == userId.Value)
                        .Select(r => (double?)r.Value)
                        .FirstOrDefault())
                    : orderedQueryable.ThenByDescending(x => x.Ratings
                        .OfType<UserRating>()
                        .Where(r => !userId.HasValue || r.UserId == userId.Value)
                        .Select(r => (double?)r.Value)
                        .FirstOrDefault()),
                MediaOrderingOption.OriginalTitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.OriginalTitle)
                    : orderedQueryable.ThenBy(x => x.OriginalTitle),
                MediaOrderingOption.OriginalTitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.OriginalTitle)
                    : orderedQueryable.ThenByDescending(x => x.OriginalTitle),
                MediaOrderingOption.PlayCountAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault())
                    : orderedQueryable.ThenBy(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault()),
                MediaOrderingOption.PlayCountDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault())
                    : orderedQueryable.ThenByDescending(x => x.UserMediaStates
                        .Where(s => !userId.HasValue || s.UserId == userId.Value)
                        .Select(s => s.PlayCount)
                        .FirstOrDefault()),
                MediaOrderingOption.PopularityAsc => throw new NotImplementedException(),
                MediaOrderingOption.PopularityDesc => throw new NotImplementedException(),
                MediaOrderingOption.TrendingDesc => MediaOrderingHelper.ApplyTrendingOrder(
                    queryable, orderedQueryable, descending: true, applicableProviders, recentCutoff, providerRatings),
                MediaOrderingOption.TrendingAsc => MediaOrderingHelper.ApplyTrendingOrder(
                    queryable, orderedQueryable, descending: false, applicableProviders, recentCutoff, providerRatings),
                MediaOrderingOption.ProviderRatingDesc => MediaOrderingHelper.ApplyProviderRatingOrder(
                    queryable, orderedQueryable, descending: true, applicableProviders, providerRatings),
                MediaOrderingOption.ProviderRatingAsc => MediaOrderingHelper.ApplyProviderRatingOrder(
                    queryable, orderedQueryable, descending: false, applicableProviders, providerRatings),
                MediaOrderingOption.ReleaseDateAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.ReleaseDate)
                    : orderedQueryable.ThenBy(x => x.ReleaseDate),
                MediaOrderingOption.ReleaseDateDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.ReleaseDate)
                    : orderedQueryable.ThenByDescending(x => x.ReleaseDate),
                MediaOrderingOption.TitleAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => x.Title)
                    : orderedQueryable.ThenBy(x => x.Title),
                MediaOrderingOption.TitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.Title)
                    : orderedQueryable.ThenByDescending(x => x.Title),
                MediaOrderingOption.TrackNumberAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => (x as MusicTrack)!.TrackNumber)
                    : orderedQueryable.ThenBy(x => (x as MusicTrack)!.TrackNumber),
                MediaOrderingOption.TrackNumberDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => (x as MusicTrack)!.TrackNumber)
                    : orderedQueryable.ThenByDescending(x => (x as MusicTrack)!.TrackNumber),
                MediaOrderingOption.DiscNumberAsc => orderedQueryable == null ?
                    queryable.OrderBy(x => (x as MusicTrack)!.DiscNumber)
                    : orderedQueryable.ThenBy(x => (x as MusicTrack)!.DiscNumber),
                MediaOrderingOption.DiscNumberDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => (x as MusicTrack)!.DiscNumber)
                    : orderedQueryable.ThenByDescending(x => (x as MusicTrack)!.DiscNumber),
                _ => throw new InvalidOperationException($"Unsupported media ordering option: {order}")
            };
        }
        return orderedQueryable!;
    }
}
