using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal sealed class HomeFeedRecentlyAddedStrategy(
    IApplicationDbContext context,
    MediaAccessFilter mediaAccessFilter)
{
    public async Task<PaginatedList<HomeFeedItemDto>> HandleAsync(
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

        var rawItems = await LoadRecentlyAddedMediasAsync(pageIds, cancellationToken);

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

        var pictureSizes = await HomeFeedQueryFilters.GetPictureSizesAsync(context, rawItems, cancellationToken);
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

        query = HomeFeedQueryFilters.ApplyFamilyFilter(query, request.MediaTypes);
        query = mediaAccessFilter.ApplyUnavailablePeerExclusion(query);

        if (userId.HasValue)
            query = await HomeFeedQueryFilters.ApplyUserExclusionsAsync(mediaAccessFilter, query, userId.Value, cancellationToken);

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

        mediaQuery = mediaAccessFilter.ApplyUnavailablePeerExclusion(mediaQuery);

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

        var itemsById = items.ToDictionary(m => m.Id);
        return pageIds.Select(id => itemsById[id]).ToList();
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
                    result.Add((insertOrder++, HomeFeedItemMapper.MapTopLevelItem(item, detailed, pictureSizes)));
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
                Genres = detailed && HomeFeedItemMapper.GetGenres(serie).Count > 0 ? HomeFeedItemMapper.GetGenres(serie) : null,
                ContentRating = detailed ? HomeFeedItemMapper.GetContentRating(serie) : null,
                RuntimeMinutes = detailed ? ep.Runtime : null,
                Rating = detailed ? HomeFeedItemMapper.GetBestRating(serie) : null
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
                Genres = detailed && HomeFeedItemMapper.GetGenres(serie).Count > 0 ? HomeFeedItemMapper.GetGenres(serie) : null,
                ContentRating = detailed ? HomeFeedItemMapper.GetContentRating(serie) : null,
                Rating = detailed ? HomeFeedItemMapper.GetBestRating(serie) : null
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
            Genres = detailed && HomeFeedItemMapper.GetGenres(serie).Count > 0 ? HomeFeedItemMapper.GetGenres(serie) : null,
            ContentRating = detailed ? HomeFeedItemMapper.GetContentRating(serie) : null,
            Rating = detailed ? HomeFeedItemMapper.GetBestRating(serie) : null
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
}
