using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

public record GetHomeFeedItemsQuery : IRequest<PaginatedList<HomeFeedItemDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public bool? ContinueWatching { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public bool? Detailed { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 20;
}

public class GetHomeFeedItemsQueryHandler(IApplicationDbContext context, IUser currentUser, IMemoryCache cache, IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<GetHomeFeedItemsQuery, PaginatedList<HomeFeedItemDto>>
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan ContinueWatchingCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<PaginatedList<HomeFeedItemDto>> Handle(GetHomeFeedItemsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var strategy = InferStrategy(request);
        var cacheKey = BuildCacheKey(request, userId);
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

    private static string BuildCacheKey(GetHomeFeedItemsQuery request, Guid? userId)
    {
        var parts = new List<string> { "home-feed", $"u:{userId}" };

        if (request.LibraryIds is { Length: > 0 })
            parts.Add($"lib:{string.Join(',', request.LibraryIds.OrderBy(x => x))}");
        if (request.ContinueWatching.HasValue)
            parts.Add($"cw:{request.ContinueWatching.Value}");
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

        var query = context.Medias
            .AsNoTracking()
            .Where(x => !(x is MusicAlbum) && !(x is MusicTrack))
            .Where(x => x.UserMediaStates.Any(s =>
                s.UserId == userId.Value
                && !s.IsCompleted
                && s.LastInteractedAt != null));

        query = ApplyFamilyFilter(query, request.MediaTypes);
        query = ApplyLibraryFilter(query, request.LibraryIds);
        query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UserMediaStates
                .Where(s => s.UserId == userId.Value)
                .Select(s => s.LastInteractedAt)
                .FirstOrDefault())
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(x => x.Pictures).ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures).ThenInclude(p => p.Variants)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures).ThenInclude(p => p.Variants)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Seasons)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var feedItems = items.Select(i => MapContinueWatchingItem(i, request.Detailed == true)).ToList();
        return new PaginatedList<HomeFeedItemDto>(feedItems, feedItems.Count, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleRecentlyAddedAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        // Fetch child-level items (episodes, tracks, movies) ordered by created desc.
        // Then aggregate: group episodes by serie/season, tracks by album.
        var query = context.Medias
            .AsNoTracking()
            .Where(x => x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any() || x is MusicAlbum || x is Serie);

        query = ApplyFamilyFilter(query, request.MediaTypes);
        query = ApplyLibraryFilter(query, request.LibraryIds);

        if (userId.HasValue)
            query = await ApplyUserExclusionsAsync(query, userId.Value, cancellationToken);

        // Fetch more than needed because aggregation reduces item count
        var fetchSize = request.PageSize * 3;
        var skip = (request.PageNumber - 1) * fetchSize;

        var rawItems = await query
            .OrderByDescending(x => x.Id)
            .Skip(skip)
            .Take(fetchSize)
            .Include(x => x.Pictures).ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Pictures).ThenInclude(p => p.Variants)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Ratings)
            .Include(x => ((SerieEpisode)x).Season).ThenInclude(s => s.Pictures).ThenInclude(p => p.Variants)
            .Include(x => ((SerieEpisode)x).Serie).ThenInclude(s => s.Seasons)
            .Include(x => ((MusicTrack)x).Album).ThenInclude(a => a.Pictures).ThenInclude(p => p.Variants)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

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

        var aggregated = AggregateRecentItems(rawItems, request.Detailed == true);
        var page = aggregated.Take(request.PageSize).ToList();
        var totalCount = aggregated.Count;

        return new PaginatedList<HomeFeedItemDto>(page, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<PaginatedList<HomeFeedItemDto>> HandleTopLevelAsync(
        GetHomeFeedItemsQuery request, Guid? userId, CancellationToken cancellationToken)
    {
        // Only top-level entities: Movie, Serie, MusicAlbum
        var query = context.Medias
            .AsNoTracking()
            .Where(x => x is Movie || x is Serie || x is MusicAlbum);

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
            .Include(x => x.Pictures).ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
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

        var feedItems = pageIds
            .Select(id => items.First(m => m.Id == id))
            .Select(m => MapTopLevelItem(m, request.Detailed == true))
            .ToList();

        return new PaginatedList<HomeFeedItemDto>(feedItems, feedItems.Count, request.PageNumber, request.PageSize);
    }

    private List<HomeFeedItemDto> AggregateRecentItems(List<BaseMedia> rawItems, bool detailed)
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
                case Serie or SerieSeason when item.PeerServerId is null:
                    // Skip local top-level serie/season entries -- episodes represent them
                    break;
                case MusicAlbum when item.PeerServerId is null:
                    // Skip local top-level album entries -- tracks represent them
                    break;
                default:
                    result.Add((insertOrder++, MapTopLevelItem(item, detailed)));
                    break;
            }
        }

        foreach (var (serieId, (order, episodes)) in serieGroups)
        {
            result.Add((order, AggregateSerieEpisodes(serieId, episodes, detailed)));
        }

        foreach (var (albumId, (order, tracks)) in albumGroups)
        {
            result.Add((order, AggregateAlbumTracks(albumId, tracks)));
        }

        return result.OrderBy(x => x.Order).Select(x => x.Item).ToList();
    }

    private static HomeFeedItemDto AggregateSerieEpisodes(Guid serieId, List<SerieEpisode> episodes, bool detailed)
    {
        var first = episodes[0];
        var serie = first.Serie!;
        var allWatched = episodes.All(e =>
            e.UserMediaStates.Any(s => s.IsCompleted));

        var distinctSeasons = episodes.Select(e => e.Season?.SeasonNumber ?? 0).Distinct().ToList();
        var isSingleSeason = distinctSeasons.Count == 1;
        var serieHasMultipleSeasons = serie.Seasons?.Count > 1;

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
                Pictures = seasonPictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
                AdditionalInfo = $"S{ep.Season?.SeasonNumber ?? 0:D2}E{ep.EpisodeNumber:D2}",
                GroupCount = 1,
                ReleaseDate = serie.ReleaseDate,
                Watched = allWatched,
                Overview = detailed ? (serie.Overview ?? ep.Overview) : null,
                Genres = detailed && serie.Genres.Count > 0 ? serie.Genres.ToList() : null,
                ContentRating = detailed ? serie.ContentRating : null,
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
                Pictures = (season?.Pictures ?? serie.Pictures)?.Select(p => p.ToMetadataPictureDto()).ToList(),
                AdditionalInfo = $"{episodes.Count} episodes",
                GroupCount = episodes.Count,
                ReleaseDate = serie.ReleaseDate,
                Watched = allWatched,
                Overview = detailed ? serie.Overview : null,
                Genres = detailed && serie.Genres.Count > 0 ? serie.Genres.ToList() : null,
                ContentRating = detailed ? serie.ContentRating : null,
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
            Pictures = serie.Pictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
            AdditionalInfo = $"{episodes.Count} episodes",
            GroupCount = episodes.Count,
            ReleaseDate = serie.ReleaseDate,
            Watched = allWatched,
            Overview = detailed ? serie.Overview : null,
            Genres = detailed && serie.Genres.Count > 0 ? serie.Genres.ToList() : null,
            ContentRating = detailed ? serie.ContentRating : null,
            Rating = detailed ? GetBestRating(serie) : null
        };
    }

    private static HomeFeedItemDto AggregateAlbumTracks(Guid albumId, List<MusicTrack> tracks)
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
            Pictures = album.Pictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
            AdditionalInfo = tracks.Count > 1 ? $"{tracks.Count} tracks" : null,
            GroupCount = tracks.Count,
            ReleaseDate = album.ReleaseDate,
            Watched = allWatched
        };
    }

    private static HomeFeedItemDto MapTopLevelItem(BaseMedia item, bool detailed = false)
    {
        var pictures = item.Pictures?.Select(p => p.ToMetadataPictureDto()).ToList();
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
            Genres = detailed && item.Genres.Count > 0 ? item.Genres.ToList() : null,
            ContentRating = detailed ? GetContentRating(item) : null,
            RuntimeMinutes = detailed ? GetRuntimeMinutes(item) : null,
            Rating = detailed ? GetBestRating(item) : null
        };
    }

    private static HomeFeedItemDto MapContinueWatchingItem(BaseMedia item, bool detailed = false)
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
                pictures = episode.Serie?.Pictures ?? item.Pictures;
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
            Pictures = pictures?.Select(p => p.ToMetadataPictureDto()).ToList(),
            AdditionalInfo = additionalInfo,
            ReleaseDate = item.ReleaseDate,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            GroupCount = 1,
            Overview = detailed ? GetOverview(detailSource) : null,
            Genres = detailed && detailSource.Genres.Count > 0 ? detailSource.Genres.ToList() : null,
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

    private static IQueryable<BaseMedia> ApplyLibraryFilter(IQueryable<BaseMedia> query, Guid[]? libraryIds)
    {
        if (libraryIds is not { Length: > 0 })
            return query;

        return query.Where(x =>
            x is MusicAlbum
                ? x.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))
                    || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                        || t.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId)))
                : x is MusicArtist
                    ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))
                        || a.Tracks.Any(t => t.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))))
                : x is Serie
                    ? x.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))
                        || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                            || e.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))))
                    : x is SerieSeason
                        ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                            || e.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId)))
                        : x.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                            || x.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId)));
    }

    private async Task<IQueryable<BaseMedia>> ApplyUserExclusionsAsync(
        IQueryable<BaseMedia> query, Guid userId, CancellationToken cancellationToken)
    {
        var excludedLibraryIds = context.UserLibraryExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId);

        query = query.Where(x =>
            x is MusicAlbum
                ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                    || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                        || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                : x is MusicArtist
                    ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || a.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
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
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId);

        query = query.Where(x => !excludedMediaIds.Contains(x.Id));

        var restrictionProfile = await context.ContentRestrictionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);

        if (restrictionProfile is not null)
            query = ContentRestrictionEvaluator.ApplyRestriction(query, restrictionProfile);

        return query;
    }

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
                MediaOrderingOption.TitleAsc => ordered?.ThenBy(x => x.Title) ?? queryable.OrderBy(x => x.Title),
                MediaOrderingOption.TitleDesc => ordered?.ThenByDescending(x => x.Title) ?? queryable.OrderByDescending(x => x.Title),
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
            .Include(x => x.Pictures).ThenInclude(p => p.Variants)
            .Include(x => x.Ratings)
            .Include(x => x.UserMediaStates.Where(s => s.UserId == userId.Value))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var feedItems = items.Select(i => MapTopLevelItem(i, request.Detailed == true)).ToList();
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

    private static string? GetContentRating(BaseMedia item) => item switch
    {
        Movie m => m.ContentRating,
        Serie s => s.ContentRating,
        _ => null
    };

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
