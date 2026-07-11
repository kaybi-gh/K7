using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

public record GetHomeFeedItemsQuery : IRequest<PaginatedList<HomeFeedItemDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public bool? ContinueWatching { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public bool? Detailed { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
}

public class GetHomeFeedItemsQueryHandler(IApplicationDbContext context, IUser currentUser, IMemoryCache cache, IMediaQueryCacheInvalidator cacheInvalidator, MediaAccessFilter mediaAccessFilter, IPlaybackPolicySettingsProvider playbackPolicySettingsProvider)
    : IRequestHandler<GetHomeFeedItemsQuery, PaginatedList<HomeFeedItemDto>>
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan ContinueWatchingCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<PaginatedList<HomeFeedItemDto>> Handle(GetHomeFeedItemsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var strategy = InferStrategy(request);
        VideoPlaybackPolicySettingsDto? continueWatchingPolicy = null;
        if (strategy == FeedStrategy.ContinueWatching && userId.HasValue)
            continueWatchingPolicy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);

        var cacheKey = BuildCacheKey(request, userId, continueWatchingPolicy);
        var version = cacheInvalidator.Version;

        if (cache.TryGetValue(cacheKey, out (long Version, PaginatedList<HomeFeedItemDto> Result) cached) && cached.Version == version)
            return cached.Result;

        var result = strategy switch
        {
            FeedStrategy.ContinueWatching => await HandleContinueWatchingAsync(request, userId, cancellationToken),
            FeedStrategy.RecentlyAdded => await HandleRecentlyAddedAsync(request, userId, cancellationToken),
            FeedStrategy.RecommendedForYou => await HandleRecommendedForYouAsync(request, userId, cancellationToken),
            _ => await HandleTopLevelAsync(request, userId, cancellationToken)
        };

        var ttl = strategy == FeedStrategy.ContinueWatching ? ContinueWatchingCacheDuration : DefaultCacheDuration;
        cache.Set(cacheKey, (version, result), ttl);
        return result;
    }

    private static FeedStrategy InferStrategy(GetHomeFeedItemsQuery request)
    {
        if (request.ContinueWatching == true)
            return FeedStrategy.ContinueWatching;

        if (request.OrderBy is { Count: > 0 } && request.OrderBy.Contains(MediaOrderingOption.RecommendedForYou))
            return FeedStrategy.RecommendedForYou;

        if (request.OrderBy is { Count: > 0 } && request.OrderBy.Contains(MediaOrderingOption.CreatedDesc))
            return FeedStrategy.RecentlyAdded;

        return FeedStrategy.TopLevel;
    }

    private static string BuildCacheKey(GetHomeFeedItemsQuery request, Guid? userId, VideoPlaybackPolicySettingsDto? continueWatchingPolicy = null)
    {
        var parts = new List<string> { "home-feed", $"u:{userId}" };

        if (request.LibraryIds is { Length: > 0 })
            parts.Add($"lib:{string.Join(',', request.LibraryIds.OrderBy(x => x))}");
        if (request.LibraryGroupIds is { Length: > 0 })
            parts.Add($"lg:{string.Join(',', request.LibraryGroupIds.OrderBy(x => x))}");
        if (request.ContinueWatching.HasValue)
            parts.Add($"cw:{request.ContinueWatching.Value}");
        if (continueWatchingPolicy is not null)
            parts.Add($"cwMin:{continueWatchingPolicy.MinResumePercent}:{continueWatchingPolicy.MinResumeDurationSeconds}:{continueWatchingPolicy.ContinueWatchingMaxAgeDays}");
        if (request.MediaTypes is { Count: > 0 })
            parts.Add($"mt:{string.Join(',', request.MediaTypes.Order())}");
        if (request.OrderBy is { Count: > 0 })
            parts.Add($"ob:{string.Join(',', request.OrderBy.Order())}");
        if (request.Detailed == true)
            parts.Add("detailed");
        parts.Add($"p:{request.PageNumber}");
        parts.Add($"ps:{request.PageSize}");

        return string.Join('|', parts);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleContinueWatchingAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var videoPolicy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var query = context.Medias
            .AsNoTracking()
            .WhereEligibleForContinueWatching(userId.Value, videoPolicy, utcNow)
            .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());

        query = ApplyFamilyFilter(query, request.MediaTypes);
        query = ApplyLibraryFilter(query, request.LibraryIds);
        query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UserMediaStates
                .Where(s => s.UserId == userId.Value)
                .Select(s => s.LastInteractedAt)
                .FirstOrDefault())
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var deduplicated = ContinueWatchingEpisodeSelector.DeduplicateBySerie(items);
        var totalCount = deduplicated.Count;
        var page = deduplicated
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var pictureSizes = await GetPictureSizesAsync(page, cancellationToken);
        var feedItems = page.Select(i => MapContinueWatchingItem(i, request.Detailed == true, pictureSizes)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleRecentlyAddedAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        // Fetch child-level items (episodes, tracks, movies) ordered by created desc.
        // Then aggregate: group episodes by serie/season, tracks by album.
        var fetchSize = request.PageSize * 3;
        var skip = (request.PageNumber - 1) * fetchSize;
        var leafTypes = ResolveLeafMediaTypes(request.MediaTypes);

        var pageIds = request.LibraryIds is { Length: > 0 }
            ? await ResolveRecentlyAddedMediaIdsFromLibrariesAsync(
                request.LibraryIds, leafTypes, userId, skip, fetchSize, cancellationToken)
            : await ResolveRecentlyAddedMediaIdsFromAllMediaAsync(
                request, leafTypes, userId, skip, fetchSize, cancellationToken);

        if (pageIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var rawItems = await LoadRecentlyAddedMediasAsync(pageIds, userId, cancellationToken);

        if (userId.HasValue)
        {
            var rawIds = rawItems.Select(x => x.Id).ToList();
            var userStates = await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId.Value && rawIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            foreach (var item in rawItems)
            {
                if (userStates.TryGetValue(item.Id, out var state))
                    item.UserMediaStates = [state];
            }
        }

        var serieSeasonCounts = await SerieSeasonCountHelper.GetCountsBySerieIdsAsync(
            context,
            SerieSeasonCountHelper.ExtractSerieIdsFromMedias(rawItems),
            cancellationToken);

        var pictureSizes = await GetPictureSizesAsync(rawItems, cancellationToken);
        var aggregated = AggregateRecentItems(rawItems, request.Detailed == true, serieSeasonCounts, pictureSizes);
        var page = aggregated.Take(request.PageSize).ToList();
        var totalCount = aggregated.Count;

        return new PaginatedList<HomeFeedItemDto>(page, totalCount, request.PageNumber, request.PageSize);
    }

    private static HashSet<MediaType> ResolveLeafMediaTypes(HashSet<MediaType>? mediaTypes)
    {
        if (mediaTypes is not { Count: > 0 })
            return [MediaType.Movie, MediaType.MusicTrack, MediaType.SerieEpisode];

        var result = new HashSet<MediaType>();
        if (mediaTypes.Contains(MediaType.Movie))
            result.Add(MediaType.Movie);
        if (mediaTypes.Contains(MediaType.MusicAlbum) || mediaTypes.Contains(MediaType.MusicTrack))
            result.Add(MediaType.MusicTrack);
        if (mediaTypes.Contains(MediaType.Serie)
            || mediaTypes.Contains(MediaType.SerieSeason)
            || mediaTypes.Contains(MediaType.SerieEpisode))
            result.Add(MediaType.SerieEpisode);

        return result;
    }

    private async Task<List<Guid>> ResolveRecentlyAddedMediaIdsFromLibrariesAsync(
        Guid[] libraryIds,
        HashSet<MediaType> leafTypes,
        Guid? userId,
        int skip,
        int fetchSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Guid>? excludedLibraryIds = null;
        if (userId.HasValue)
        {
            excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);
        }

        var indexedEntries = context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId != null && libraryIds.Contains(f.LibraryId));

        if (excludedLibraryIds is not null)
            indexedEntries = indexedEntries.Where(f => !excludedLibraryIds.Contains(f.LibraryId));

        var remoteEntries = context.RemoteIndexedFiles
            .AsNoTracking()
            .Where(f => libraryIds.Contains(f.LibraryId));

        if (excludedLibraryIds is not null)
            remoteEntries = remoteEntries.Where(f => !excludedLibraryIds.Contains(f.LibraryId));

        var candidateIds = await indexedEntries
            .Select(f => new { MediaId = f.MediaId!.Value, f.Created })
            .Concat(remoteEntries.Select(f => new { f.MediaId, f.Created }))
            .GroupBy(x => x.MediaId)
            .Select(g => new { MediaId = g.Key, Created = g.Max(x => x.Created) })
            .OrderByDescending(x => x.Created)
            .Skip(skip)
            .Take(fetchSize * 2)
            .Select(x => x.MediaId)
            .ToListAsync(cancellationToken);

        return await FilterRecentlyAddedMediaIdsAsync(
            candidateIds, leafTypes, userId, fetchSize, cancellationToken);
    }

    private async Task<List<Guid>> ResolveRecentlyAddedMediaIdsFromAllMediaAsync(
        GetHomeFeedItemsQuery request,
        HashSet<MediaType> leafTypes,
        Guid? userId,
        int skip,
        int fetchSize,
        CancellationToken cancellationToken)
    {
        var query = context.Medias
            .AsNoTracking()
            .Where(x => leafTypes.Contains(x.Type))
            .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());

        query = ApplyFamilyFilter(query, request.MediaTypes);

        if (userId.HasValue)
            query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        return await query
            .OrderByDescending(x => x.Id)
            .Skip(skip)
            .Take(fetchSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> FilterRecentlyAddedMediaIdsAsync(
        List<Guid> candidateIds,
        HashSet<MediaType> leafTypes,
        Guid? userId,
        int fetchSize,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0 || leafTypes.Count == 0)
            return [];

        var mediaQuery = context.Medias
            .AsNoTracking()
            .Where(m => candidateIds.Contains(m.Id) && leafTypes.Contains(m.Type));

        if (userId.HasValue)
        {
            var excludedMediaIds = context.UserMediaExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.MediaId);

            mediaQuery = mediaQuery.WhereNotUserExcluded(excludedMediaIds);

            var restrictionProfile = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId.Value), cancellationToken);

            if (restrictionProfile is not null)
                mediaQuery = ContentRestrictionEvaluator.ApplyRestriction(mediaQuery, restrictionProfile);
        }

        var filteredIds = await mediaQuery.Select(m => m.Id).ToListAsync(cancellationToken);
        var filteredSet = filteredIds.ToHashSet();
        return candidateIds.Where(id => filteredSet.Contains(id)).Take(fetchSize).ToList();
    }

    private async Task<List<BaseMedia>> LoadRecentlyAddedMediasAsync(
        List<Guid> pageIds,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var items = await context.Medias
            .Where(m => pageIds.Contains(m.Id))
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures)
            .Include(x => ((MusicTrack)x).Album).ThenInclude(a => a.Pictures)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return pageIds.Select(id => items.First(m => m.Id == id)).ToList();
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleTopLevelAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        // Only top-level entities: Movie, Serie, MusicAlbum
        var query = context.Medias
            .AsNoTracking()
            .Where(x => x is Movie || x is Serie || x is MusicAlbum);

        query = CatalogMediaAvailabilityHelper.WhereHasPlayableFiles(query);
        query = ApplyFamilyFilter(query, request.MediaTypes);
        query = ApplyLibraryFilter(query, request.LibraryIds);

        if (userId.HasValue)
            query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        var ordered = ApplyOrdering(request.OrderBy, query, userId);
        var pageIds = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (pageIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var items = await context.Medias
            .Where(m => pageIds.Contains(m.Id))
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (userId.HasValue)
        {
            var userStates = await context.UserMediaStates
                .AsNoTracking()
                .Where(s => s.UserId == userId.Value && pageIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            foreach (var item in items)
            {
                if (userStates.TryGetValue(item.Id, out var state))
                    item.UserMediaStates = [state];
            }
        }

        var pictureSizes = await GetPictureSizesAsync(items, cancellationToken);
        var feedItems = pageIds
            .Select(id => items.First(m => m.Id == id))
            .Select(m => MapTopLevelItem(m, request.Detailed == true, pictureSizes))
            .ToList();

        return new PaginatedList<HomeFeedItemDto>(feedItems, feedItems.Count, request.PageNumber, request.PageSize);
    }

    private static List<HomeFeedItemDto> AggregateRecentItems(
        List<BaseMedia> rawItems,
        bool detailed,
        IReadOnlyDictionary<Guid, int> serieSeasonCounts,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var result = new List<(int Order, HomeFeedItemDto Item)>();
        var serieGroups = new Dictionary<Guid, (int Order, List<SerieEpisode> Episodes)>();
        var albumGroups = new Dictionary<Guid, (int Order, List<MusicTrack> Tracks)>();
        var insertOrder = 0;

        foreach (var item in rawItems)
        {
            switch (item)
            {
                case SerieEpisode episode when episode.Serie is not null:
                    {
                        var serieId = episode.Serie.Id;
                        if (!serieGroups.ContainsKey(serieId))
                            serieGroups[serieId] = (insertOrder++, []);
                        serieGroups[serieId].Episodes.Add(episode);
                        break;
                    }
                case MusicTrack track when track.Album is not null:
                    {
                        var albumId = track.Album.Id;
                        if (!albumGroups.ContainsKey(albumId))
                            albumGroups[albumId] = (insertOrder++, []);
                        albumGroups[albumId].Tracks.Add(track);
                        break;
                    }
                case Serie or SerieSeason:
                    // Skip top-level serie/season entries -- episodes represent them
                    break;
                case MusicAlbum:
                    // Skip top-level album entries -- tracks represent them
                    break;
                default:
                    result.Add((insertOrder++, MapTopLevelItem(item, detailed, pictureSizes)));
                    break;
            }
        }

        foreach (var (serieId, (order, episodes)) in serieGroups)
        {
            var seasonCount = SerieSeasonCountHelper.ResolveCount(serieId, episodes[0].Serie, serieSeasonCounts);
            result.Add((order, AggregateSerieEpisodes(serieId, episodes, detailed, seasonCount, pictureSizes)));
        }

        foreach (var (albumId, (order, tracks)) in albumGroups)
        {
            result.Add((order, AggregateAlbumTracks(albumId, tracks, pictureSizes)));
        }

        return result.OrderBy(x => x.Order).Select(x => x.Item).ToList();
    }

    private static HomeFeedItemDto AggregateSerieEpisodes(
        Guid serieId,
        List<SerieEpisode> episodes,
        bool detailed,
        int serieSeasonCount,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var first = episodes[0];
        var serie = first.Serie!;
        var allWatched = episodes.All(e =>
            e.UserMediaStates.Any(s => s.IsCompleted));

        var distinctSeasons = episodes.Select(e => e.Season?.SeasonNumber ?? 0).Distinct().ToList();
        var isSingleSeason = distinctSeasons.Count == 1;
        var serieHasMultipleSeasons = serieSeasonCount > 1;

        if (episodes.Count == 1)
        {
            // Single episode (weekly) -- link to the episode anchor
            var ep = first;
            var seasonPictures = ep.Season?.Pictures ?? serie.Pictures;
            return new HomeFeedItemDto
            {
                Id = serieId,
                Title = serie.Title ?? ep.Title ?? "",
                MediaType = MediaType.SerieEpisode,
                NavigationTarget = $"/series/{serieId}/seasons/{ep.Season?.SeasonNumber ?? 0}#ep-{ep.EpisodeNumber}",
                Pictures = seasonPictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
                AdditionalInfo = $"S{ep.Season?.SeasonNumber ?? 0:D2}E{ep.EpisodeNumber:D2}",
                GroupCount = 1,
                ReleaseDate = serie.ReleaseDate,
                Watched = allWatched,
                Overview = detailed ? (serie.Overview ?? ep.Overview) : null,
                Genres = detailed && GetGenres(serie).Count > 0 ? GetGenres(serie) : null,
                ContentRating = detailed ? GetContentRating(serie) : null,
                RuntimeMinutes = detailed ? ep.Runtime : null,
                Rating = detailed ? GetBestRating(serie) : null
            };
        }

        if (isSingleSeason && serieHasMultipleSeasons == true)
        {
            // All episodes from one season of a multi-season serie -- season card
            var season = first.Season;
            var seasonNumber = distinctSeasons[0];
            return new HomeFeedItemDto
            {
                Id = serieId,
                Title = serie.Title ?? "",
                MediaType = MediaType.SerieSeason,
                NavigationTarget = $"/series/{serieId}/seasons/{seasonNumber}",
                Pictures = (season?.Pictures ?? serie.Pictures)?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
                AdditionalInfo = $"{episodes.Count} episodes",
                GroupCount = episodes.Count,
                ReleaseDate = serie.ReleaseDate,
                Watched = allWatched,
                Overview = detailed ? serie.Overview : null,
                Genres = detailed && GetGenres(serie).Count > 0 ? GetGenres(serie) : null,
                ContentRating = detailed ? GetContentRating(serie) : null,
                Rating = detailed ? GetBestRating(serie) : null
            };
        }

        // Multiple seasons or single-season serie -- serie card
        return new HomeFeedItemDto
        {
            Id = serieId,
            Title = serie.Title ?? "",
            MediaType = MediaType.Serie,
            NavigationTarget = $"/series/{serieId}",
            Pictures = serie.Pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
            AdditionalInfo = $"{episodes.Count} episodes",
            GroupCount = episodes.Count,
            ReleaseDate = serie.ReleaseDate,
            Watched = allWatched,
            Overview = detailed ? serie.Overview : null,
            Genres = detailed && GetGenres(serie).Count > 0 ? GetGenres(serie) : null,
            ContentRating = detailed ? GetContentRating(serie) : null,
            Rating = detailed ? GetBestRating(serie) : null
        };
    }

    private static HomeFeedItemDto AggregateAlbumTracks(
        Guid albumId,
        List<MusicTrack> tracks,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var first = tracks[0];
        var album = first.Album!;
        var allWatched = tracks.All(t =>
            t.UserMediaStates.Any(s => s.IsCompleted));

        return new HomeFeedItemDto
        {
            Id = albumId,
            Title = album.Title ?? first.Title ?? "",
            MediaType = MediaType.MusicAlbum,
            NavigationTarget = $"/music/albums/{albumId}",
            Pictures = album.Pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
            AdditionalInfo = tracks.Count > 1 ? $"{tracks.Count} tracks" : null,
            GroupCount = tracks.Count,
            ReleaseDate = album.ReleaseDate,
            Watched = allWatched
        };
    }

    private static HomeFeedItemDto MapTopLevelItem(
        BaseMedia item,
        bool detailed = false,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var pictures = item.Pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList();
        var userState = item.UserMediaStates.FirstOrDefault();

        return new HomeFeedItemDto
        {
            Id = item.Id,
            Title = item.Title ?? "",
            MediaType = item.Type,
            NavigationTarget = item switch
            {
                Movie => $"/movies/{item.Id}",
                Serie => $"/series/{item.Id}",
                MusicAlbum => $"/music/albums/{item.Id}",
                _ => $"/medias/{item.Id}"
            },
            Pictures = pictures,
            ReleaseDate = item.ReleaseDate,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            GroupCount = 1,
            Overview = detailed ? GetOverview(item) : null,
            Genres = detailed && GetGenres(item).Count > 0 ? GetGenres(item) : null,
            ContentRating = detailed ? GetContentRating(item) : null,
            RuntimeMinutes = detailed ? GetRuntimeMinutes(item) : null,
            Rating = detailed ? GetBestRating(item) : null
        };
    }

    private static HomeFeedItemDto MapContinueWatchingItem(
        BaseMedia item,
        bool detailed = false,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var userState = item.UserMediaStates.FirstOrDefault();
        IList<MetadataPicture>? pictures;
        string navTarget;
        string title;
        string? additionalInfo = null;
        BaseMedia detailSource = item;

        switch (item)
        {
            case SerieEpisode episode:
                pictures = EpisodePictureResolver.ResolveDisplayPictures(episode);
                navTarget = $"/series/{episode.Serie?.Id ?? item.Id}/seasons/{episode.Season?.SeasonNumber ?? 0}#ep-{episode.EpisodeNumber}";
                title = episode.Serie?.Title ?? episode.Title ?? "";
                additionalInfo = $"S{episode.Season?.SeasonNumber ?? 0:D2}E{episode.EpisodeNumber:D2}";
                detailSource = episode.Serie ?? item;
                break;
            default:
                pictures = item.Pictures;
                navTarget = item switch
                {
                    Movie => $"/movies/{item.Id}",
                    _ => $"/medias/{item.Id}"
                };
                title = item.Title ?? "";
                break;
        }

        return new HomeFeedItemDto
        {
            Id = item.Id,
            Title = title,
            MediaType = item.Type,
            NavigationTarget = navTarget,
            Pictures = pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
            AdditionalInfo = additionalInfo,
            ReleaseDate = item.ReleaseDate,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            GroupCount = 1,
            Overview = detailed ? GetOverview(detailSource) : null,
            Genres = detailed && GetGenres(detailSource).Count > 0 ? GetGenres(detailSource) : null,
            ContentRating = detailed ? GetContentRating(detailSource) : null,
            RuntimeMinutes = detailed ? GetRuntimeMinutes(item) : null,
            Rating = detailed ? GetBestRating(detailSource) : null
        };
    }

    private static IQueryable<BaseMedia> ApplyFamilyFilter(IQueryable<BaseMedia> query, HashSet<MediaType>? mediaTypes)
    {
        if (mediaTypes is not { Count: > 0 })
            return query;

        // MediaTypes acts as a family selector:
        // Serie -> include Serie, SerieSeason, SerieEpisode
        // MusicAlbum -> include MusicAlbum, MusicTrack
        // Movie -> include Movie
        var conditions = new List<Func<BaseMedia, bool>>();
        var includeMovies = mediaTypes.Contains(MediaType.Movie);
        var includeSeries = mediaTypes.Contains(MediaType.Serie)
                            || mediaTypes.Contains(MediaType.SerieEpisode)
                            || mediaTypes.Contains(MediaType.SerieSeason);
        var includeMusic = mediaTypes.Contains(MediaType.MusicAlbum)
                           || mediaTypes.Contains(MediaType.MusicTrack);

        return query.Where(x =>
            (includeMovies && x is Movie) ||
            (includeSeries && (x is Serie || x is SerieSeason || x is SerieEpisode)) ||
            (includeMusic && (x is MusicAlbum || x is MusicTrack)));
    }

    private IQueryable<BaseMedia> ApplyLibraryFilter(IQueryable<BaseMedia> query, Guid[]? libraryIds) =>
        query.WhereAvailableInLibraries(context, libraryIds ?? []);

    private Task<IQueryable<BaseMedia>> ApplyUserExclusionsAsync(
        IQueryable<BaseMedia> query, Guid userId, CancellationToken cancellationToken) =>
        mediaAccessFilter.ApplyAllAsync(query, userId, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>> GetPictureSizesAsync(
        IEnumerable<BaseMedia> medias, CancellationToken cancellationToken) =>
        await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(
            context,
            MetadataPictureSizesHelper.ExtractPictureIdsFromMedias(medias),
            cancellationToken);

    private static IOrderedQueryable<BaseMedia> ApplyOrdering(
        HashSet<MediaOrderingOption>? orderBy, IQueryable<BaseMedia> queryable, Guid? userId)
    {
        if (orderBy is not { Count: > 0 })
            return queryable.OrderByDescending(x => x.Id);

        IOrderedQueryable<BaseMedia>? ordered = null;

        foreach (var option in orderBy)
        {
            ordered = option switch
            {
                MediaOrderingOption.CreatedAsc => ordered?.ThenBy(x => x.Id) ?? queryable.OrderBy(x => x.Id),
                MediaOrderingOption.CreatedDesc => ordered?.ThenByDescending(x => x.Id) ?? queryable.OrderByDescending(x => x.Id),
                MediaOrderingOption.TitleAsc => ordered?.ThenBy(x => x.SortTitle ?? x.Title) ?? queryable.OrderBy(x => x.SortTitle ?? x.Title),
                MediaOrderingOption.TitleDesc => ordered?.ThenByDescending(x => x.SortTitle ?? x.Title) ?? queryable.OrderByDescending(x => x.SortTitle ?? x.Title),
                MediaOrderingOption.ReleaseDateAsc => ordered?.ThenBy(x => x.ReleaseDate) ?? queryable.OrderBy(x => x.ReleaseDate),
                MediaOrderingOption.ReleaseDateDesc => ordered?.ThenByDescending(x => x.ReleaseDate) ?? queryable.OrderByDescending(x => x.ReleaseDate),
                MediaOrderingOption.LocalRatingAsc => ordered?.ThenBy(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault())
                    ?? queryable.OrderBy(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault()),
                MediaOrderingOption.LocalRatingDesc => ordered?.ThenByDescending(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault())
                    ?? queryable.OrderByDescending(x => x.Ratings
                    .OfType<K7.Server.Domain.Entities.Ratings.UserRating>()
                    .Where(r => !userId.HasValue || r.UserId == userId.Value)
                    .Select(r => (double?)r.Value).FirstOrDefault()),
                _ => ordered ?? queryable.OrderByDescending(x => x.Id)
            };
        }

        return ordered!;
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleRecommendedForYouAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        // Get user's recently watched media
        var recentMediaIds = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.UserId == userId.Value && s.LastInteractedAt != null)
            .OrderByDescending(s => s.LastInteractedAt)
            .Take(10)
            .Select(s => s.MediaId)
            .ToListAsync(cancellationToken);

        if (recentMediaIds.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        // Collect recommendation external IDs from those media
        var recommendations = await context.MediaRecommendations
            .AsNoTracking()
            .Where(r => recentMediaIds.Contains(r.MediaId))
            .ToListAsync(cancellationToken);

        if (recommendations.Count == 0)
            return new PaginatedList<HomeFeedItemDto>([], 0, request.PageNumber, request.PageSize);

        var allRecommendedIds = recommendations
            .SelectMany(r => r.RecommendedIds.Select(id => new { r.ProviderName, ExternalId = id }))
            .Distinct()
            .ToList();

        var externalIdValues = allRecommendedIds.Select(x => x.ExternalId).Distinct().ToList();

        // Find local media matching those external IDs
        var query = context.Medias
            .AsNoTracking()
            .Where(m => !recentMediaIds.Contains(m.Id))
            .Where(m => m.ExternalIds.Any(e => externalIdValues.Contains(e.Value)));

        query = ApplyLibraryFilter(query, request.LibraryIds);
        query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        var items = await query
            .Take(request.PageSize)
            .Include(x => x.Pictures)
            .Include(x => x.Ratings)
            .Include(x => x.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var pictureSizes = await GetPictureSizesAsync(items, cancellationToken);
        var feedItems = items.Select(i => MapTopLevelItem(i, request.Detailed == true, pictureSizes)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, feedItems.Count, request.PageNumber, request.PageSize);
    }

    private enum FeedStrategy
    {
        ContinueWatching,
        RecentlyAdded,
        RecommendedForYou,
        TopLevel
    }

    private static string? GetOverview(BaseMedia item) => item switch
    {
        Movie m => m.Tagline ?? m.Overview,
        Serie s => s.Overview,
        SerieEpisode e => e.Overview,
        MusicAlbum a => a.Overview,
        _ => null
    };

    private static List<string> GetGenres(BaseMedia media) =>
        media.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            .Select(mt => mt.MetadataTag.DisplayName)
            .ToList();

    private static string? GetContentRating(BaseMedia item) =>
        item.MetadataTags
            .FirstOrDefault(mt => mt.MetadataTag.Kind == MetadataTagKind.ContentRating)
            ?.MetadataTag.DisplayName;

    private static int? GetRuntimeMinutes(BaseMedia item) => item switch
    {
        SerieEpisode e => e.Runtime,
        _ => null
    };

    private static double? GetBestRating(BaseMedia item)
    {
        var rating = item.Ratings
            .OfType<K7.Server.Domain.Entities.Ratings.MetadataProviderRating>()
            .FirstOrDefault();
        if (rating is null || rating.MaximumValue == 0)
            return null;
        return Math.Round(rating.Value / rating.MaximumValue * 10, 1);
    }
}
