using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
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

public record QueryMediasQuery(QueryMediasRequest Request) : IRequest<PaginatedList<BaseMedia>>;

public class GetMediasQueryHandler(IApplicationDbContext context, IUser currentUser, IMemoryCache cache, IMediaQueryCacheInvalidator cacheInvalidator, MediaAccessFilter mediaAccessFilter, IPlaybackPolicySettingsProvider playbackPolicySettingsProvider, LiteMediaProjectionService liteMediaProjection, IDatabaseCapabilities databaseCapabilities)
    : IRequestHandler<GetMediasWithPaginationQuery, PaginatedList<BaseMedia>>,
      IRequestHandler<QueryMediasQuery, PaginatedList<BaseMedia>>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private const int CacheEntrySize = 1;

    public Task<PaginatedList<BaseMedia>> Handle(QueryMediasQuery request, CancellationToken cancellationToken) =>
        HandleInternal(MapQueryRequest(request.Request), request.Request.Filter, cancellationToken);

    public Task<PaginatedList<BaseMedia>> Handle(GetMediasWithPaginationQuery request, CancellationToken cancellationToken) =>
        HandleInternal(request, filter: null, cancellationToken);

    private async Task<PaginatedList<BaseMedia>> HandleInternal(
        GetMediasWithPaginationQuery request,
        RuleGroupDto? filter,
        CancellationToken cancellationToken)
    {
        var cacheKey = await BuildCacheKeyAsync(request, currentUser.Id, filter, includePagination: true, cancellationToken);
        var version = cacheInvalidator.Version;

        if (cache.TryGetValue(cacheKey, out (long Version, PaginatedList<BaseMedia> Result) cached) && cached.Version == version)
            return cached.Result;

        var result = await ExecuteQueryAsync(request, filter, cancellationToken);
        cache.Set(cacheKey, (version, result), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = CacheEntrySize
        });
        return result;
    }

    private async Task<string> BuildCacheKeyAsync(
        GetMediasWithPaginationQuery request,
        Guid? userId,
        RuleGroupDto? filter,
        bool includePagination,
        CancellationToken cancellationToken)
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
        if (request.ContinueWatching == true && userId.HasValue)
        {
            var policy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);
            parts.Add($"cwMin:{policy.MinResumePercent}:{policy.MinResumeDurationSeconds}:{policy.ContinueWatchingMaxAgeDays}");
        }
        if (request.UnwatchedOnly.HasValue)
            parts.Add($"uw:{request.UnwatchedOnly.Value}");
        if (request.PersonIds is { Length: > 0 })
            parts.Add($"pid:{string.Join(',', request.PersonIds.OrderBy(x => x))}");
        if (request.ArtistIds is { Length: > 0 })
            parts.Add($"aid:{string.Join(',', request.ArtistIds.OrderBy(x => x))}");
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
        if (filter is { Items.Count: > 0 })
            parts.Add($"f:{HashFilter(filter)}");
        if (includePagination)
        {
            parts.Add($"p:{request.PageNumber}");
            parts.Add($"ps:{request.PageSize}");
        }

        return string.Join('|', parts);
    }

    private static string HashFilter(RuleGroupDto filter)
    {
        var json = JsonSerializer.Serialize(filter);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }

    private async Task<PaginatedList<BaseMedia>> ExecuteQueryAsync(
        GetMediasWithPaginationQuery request,
        RuleGroupDto? filter,
        CancellationToken cancellationToken)
    {
        Guid? userId = currentUser.Id;

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var filterQuery = context.Medias
            .AsNoTracking();

        filterQuery = await ApplyFiltersAsync(request, filterQuery, userId, cancellationToken);

        if (filter is { Items.Count: > 0 } filterDto)
            filterQuery = MediaRuleEvaluator.ApplyFilter(filterQuery, filterDto.ToRuleGroup(), userId);

        if (userId.HasValue)
            filterQuery = await mediaAccessFilter.ApplyAllAsync(filterQuery, userId.Value, cancellationToken);

        var version = cacheInvalidator.Version;
        var countCacheKey = await BuildCacheKeyAsync(request, userId, filter, includePagination: false, cancellationToken);
        var countCacheKeyWithVersion = $"count|v:{version}|{countCacheKey}";

        int totalCount;
        if (cache.TryGetValue(countCacheKeyWithVersion, out int cachedCount))
        {
            totalCount = cachedCount;
        }
        else
        {
            totalCount = await filterQuery.CountAsync(cancellationToken);
            cache.Set(countCacheKeyWithVersion, totalCount, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = CacheEntrySize
            });
        }

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

        var ordered = await liteMediaProjection.GetLiteMediasAsync(pageIds, userId, cancellationToken);

        return new PaginatedList<BaseMedia>(ordered, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<IQueryable<BaseMedia>> ApplyFiltersAsync(
        GetMediasWithPaginationQuery request,
        IQueryable<BaseMedia> query,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        query = ApplyFilters(context, request, query, userId, BuildSearchPattern(request.SearchText));

        if (request.ContinueWatching == true && userId.HasValue)
        {
            var policy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);
            query = query.WhereEligibleForContinueWatching(userId.Value, policy, DateTime.UtcNow);
        }

        return query;
    }

    private string? BuildSearchPattern(string? searchText) =>
        string.IsNullOrWhiteSpace(searchText)
            ? null
            : MediaTextSearchHelper.BuildTitlePattern(searchText, databaseCapabilities.SupportsTrigramSearch);

    internal static IQueryable<BaseMedia> ApplyFilters(
        IApplicationDbContext context,
        GetMediasWithPaginationQuery request,
        IQueryable<BaseMedia> query,
        Guid? userId,
        string? searchPattern = null)
    {
        var includeSeasons = request.MediaTypes?.Contains(MediaType.SerieSeason) == true;
        var includeEpisodes = request.MediaTypes?.Contains(MediaType.SerieEpisode) == true;
        query = query.Where(x => x is MusicAlbum || x is MusicArtist || x is MusicTrack || x is Serie || (includeSeasons && x is SerieSeason)
            || (includeEpisodes && x is SerieEpisode)
            || context.MediaLibraryAvailabilities.Any(a => a.MediaId == x.Id));

        if (request.LibraryIds?.Length > 0)
            query = query.WhereAvailableInLibraries(context, request.LibraryIds);

        if (request.Ids?.Length > 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
        }

        if (request.MediaTypes?.Count > 0)
        {
            query = query.Where(x => request.MediaTypes.Contains(x.Type));
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
            query = query.Where(x => x.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && request.Genres.Contains(mt.MetadataTag.DisplayName))
                || (x is MusicTrack && ((MusicTrack)x).Album.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && request.Genres.Contains(mt.MetadataTag.DisplayName)))
                || (x is SerieSeason && ((SerieSeason)x).Serie.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && request.Genres.Contains(mt.MetadataTag.DisplayName)))
                || (x is SerieEpisode && ((SerieEpisode)x).Serie.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && request.Genres.Contains(mt.MetadataTag.DisplayName)))
                || (x is MusicArtist && ((MusicArtist)x).Albums.Any(a => a.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && request.Genres.Contains(mt.MetadataTag.DisplayName)))));
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

        if (searchPattern is not null)
        {
            query = query.Where(x => x.Title != null && EF.Functions.Like(x.Title.ToLower(), searchPattern));
        }

        if (request.UnwatchedOnly == true && userId.HasValue)
        {
            query = query.Where(x => !x.UserMediaStates.Any(s => s.UserId == userId.Value && s.IsCompleted));
        }

        return query;
    }

    private static GetMediasWithPaginationQuery MapQueryRequest(QueryMediasRequest request) => new()
    {
        LibraryIds = request.LibraryIds,
        LibraryGroupIds = request.LibraryGroupIds,
        Ids = request.Ids,
        MediaTypes = ToEnumHashSet<MediaType>(request.MediaTypes),
        OrderBy = ToEnumHashSet<MediaOrderingOption>(request.OrderBy),
        Provenance = request.Provenance,
        SearchText = request.SearchText,
        PageNumber = request.PageNumber,
        PageSize = request.PageSize
    };

    private static EnumHashSetQueryParam<TEnum>? ToEnumHashSet<TEnum>(HashSet<TEnum>? values)
        where TEnum : struct, Enum
    {
        if (values is not { Count: > 0 })
            return null;

        var result = new EnumHashSetQueryParam<TEnum>();
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(
        HashSet<MediaOrderingOption>? orderBy,
        IQueryable<BaseMedia> queryable,
        Guid? userId,
        HashSet<MetadataProvider> applicableProviders,
        DateTime recentCutoff,
        IQueryable<MetadataProviderRating> providerRatings)
    {
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
                    queryable.OrderBy(x => x.SortTitle ?? x.Title)
                    : orderedQueryable.ThenBy(x => x.SortTitle ?? x.Title),
                MediaOrderingOption.TitleDesc => orderedQueryable == null ?
                    queryable.OrderByDescending(x => x.SortTitle ?? x.Title)
                    : orderedQueryable.ThenByDescending(x => x.SortTitle ?? x.Title),
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

        return orderedQueryable!.ThenBy(x => x.Id);
    }
}
