using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;

namespace K7.Server.Application.Features.Medias.Commands.BulkCreateMedias;

[Authorize(Roles = Roles.Administrator)]
public record BulkCreateMediasCommand : IRequest<BulkCreateMediasResponse>
{
    public required IReadOnlyList<BulkCreateMediasRequest.BulkCreateMediaItem> Items { get; init; }
    public bool FetchMetadata { get; init; }
    public bool CreateMissing { get; init; } = true;
}

public class BulkCreateMediasCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IEnumerable<IMetadataProviderInfo> metadataProviders,
    MediaIdentityLookupService identityLookup)
    : IRequestHandler<BulkCreateMediasCommand, BulkCreateMediasResponse>
{
    private const int SaveBatchSize = 500;

    public async Task<BulkCreateMediasResponse> Handle(BulkCreateMediasCommand request, CancellationToken cancellationToken)
    {
        var resultMap = new Dictionary<string, (Guid MediaId, bool WasCreated)>();

        // 1. ExternalId dedup: find existing media by provider IDs
        var itemsWithExternalIds = request.Items
            .Where(i => i.ExternalIds.Count > 0)
            .ToList();

        var externalIdLookup = await identityLookup.LookupByExternalIdsAsync(itemsWithExternalIds, cancellationToken);

        foreach (var item in itemsWithExternalIds)
        {
            foreach (var (provider, value) in OrderedExternalIds(item))
            {
                if (!externalIdLookup.TryGetValue((provider.ToLowerInvariant(), value), out var hit))
                    continue;

                if (!ImportMediaTypeCompatibility.IsCompatible(item.MediaType, hit.Type))
                    continue;

                resultMap.TryAdd(item.Key, (hit.MediaId, false));
                break;
            }
        }

        // Music: an earlier import may have attached Spotify (etc.) ids to virtual file-less
        // tracks. Do not let those block title matching against the real library copy.
        // Keep the virtual hit and restore it when title matching finds nothing.
        var virtualMusicHits = new Dictionary<string, (Guid MediaId, bool WasCreated)>();
        if (resultMap.Count > 0)
        {
            var musicKeys = request.Items
                .Where(i => i.MediaType == "music" && resultMap.ContainsKey(i.Key))
                .Select(i => i.Key)
                .ToList();

            if (musicKeys.Count > 0)
            {
                var mediaIds = musicKeys.Select(k => resultMap[k].MediaId).Distinct().ToList();
                var playableIds = (await context.Medias
                    .Where(m => mediaIds.Contains(m.Id) && m.IndexedFiles.Any())
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();

                foreach (var key in musicKeys)
                {
                    if (playableIds.Contains(resultMap[key].MediaId))
                        continue;

                    virtualMusicHits[key] = resultMap[key];
                    resultMap.Remove(key);
                }
            }
        }

        // 2. Title-based dedup for music items without a playable ExternalId match
        var unmatchedMusic = request.Items
            .Where(i => i.MediaType == "music" && !resultMap.ContainsKey(i.Key))
            .ToList();

        if (unmatchedMusic.Count > 0)
        {
            var titleLookup = await identityLookup.LookupMusicByTitleAsync(unmatchedMusic, cancellationToken);
            foreach (var item in unmatchedMusic)
            {
                var titleKey = MediaIdentityKeys.NormalizeMusicTitle(item.ArtistName, item.Title);
                if (titleLookup.TryGetValue(titleKey, out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                }
            }
        }

        foreach (var (key, hit) in virtualMusicHits)
            resultMap.TryAdd(key, hit);

        await PropagateMusicGroupMatchesAsync(request.Items, resultMap, cancellationToken);

        var unmatchedMovies = request.Items
            .Where(i => i.MediaType == "movie" && !resultMap.ContainsKey(i.Key))
            .ToList();

        if (unmatchedMovies.Count > 0)
        {
            var movieLookup = await identityLookup.LookupMoviesByTitleYearAsync(unmatchedMovies, cancellationToken);
            foreach (var item in unmatchedMovies)
            {
                var titleKey = MediaIdentityKeys.NormalizeMovieTitle(item.Title, item.Year);
                if (movieLookup.TryGetValue(titleKey, out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                }
            }
        }

        var unmatchedEpisodes = request.Items
            .Where(i => i.MediaType == "episode" && !resultMap.ContainsKey(i.Key))
            .ToList();

        if (unmatchedEpisodes.Count > 0)
        {
            var episodeLookup = await identityLookup.LookupEpisodesByIdentityAsync(unmatchedEpisodes, cancellationToken);
            foreach (var item in unmatchedEpisodes)
            {
                var titleKey = MediaIdentityKeys.NormalizeEpisodeKey(item.SeriesTitle, item.SeasonNumber, item.EpisodeNumber, item.Title);
                if (episodeLookup.TryGetValue(titleKey, out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                }
            }
        }

        var unmatchedSeries = request.Items
            .Where(i => i.MediaType == "serie" && !resultMap.ContainsKey(i.Key))
            .ToList();

        if (unmatchedSeries.Count > 0)
        {
            var serieLookup = await identityLookup.LookupSeriesByTitleYearAsync(unmatchedSeries, cancellationToken);
            foreach (var item in unmatchedSeries)
            {
                var titleKey = MediaIdentityKeys.NormalizeSerieTitle(item.Title, item.Year);
                if (serieLookup.TryGetValue(titleKey, out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                }
            }
        }

        if (!request.CreateMissing)
        {
            return new BulkCreateMediasResponse
            {
                Results = request.Items.Select(i =>
                {
                    var (mediaId, wasCreated) = resultMap.GetValueOrDefault(i.Key);
                    return new BulkCreateMediasResponse.BulkCreateMediaResult
                    {
                        Key = i.Key,
                        MediaId = mediaId,
                        WasCreated = wasCreated
                    };
                }).Where(r => r.MediaId != Guid.Empty).ToList()
            };
        }

        // 3. Create missing media, grouped by type
        var toCreate = request.Items
            .Where(i => !resultMap.ContainsKey(i.Key))
            .ToList();

        var batchGroups = GroupForIntraBatchDedup(toCreate);
        var newEnrichableMediaIds = new List<Guid>();

        await CreateMoviesAsync(batchGroups, resultMap, newEnrichableMediaIds, cancellationToken);
        await CreateMusicAsync(batchGroups, resultMap, newEnrichableMediaIds, cancellationToken);
        await CreateEpisodesAsync(batchGroups, resultMap, newEnrichableMediaIds, cancellationToken);
        await CreateSeriesAsync(batchGroups, resultMap, newEnrichableMediaIds, cancellationToken);

        if (request.FetchMetadata && newEnrichableMediaIds.Count > 0)
        {
            await QueueMetadataRefreshAsync(newEnrichableMediaIds, cancellationToken);
        }

        return new BulkCreateMediasResponse
        {
            Results = request.Items.Select(i =>
            {
                var (mediaId, wasCreated) = resultMap.GetValueOrDefault(i.Key);
                return new BulkCreateMediasResponse.BulkCreateMediaResult
                {
                    Key = i.Key,
                    MediaId = mediaId,
                    WasCreated = wasCreated
                };
            }).Where(r => r.MediaId != Guid.Empty).ToList()
        };
    }

    private async Task CreateMoviesAsync(
        List<BatchGroup> batchGroups,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        List<Guid> newEnrichableMediaIds,
        CancellationToken cancellationToken)
    {
        var movieGroups = batchGroups.Where(g => g.MediaType == "movie").ToList();
        var pending = new List<(Movie Entity, BatchGroup Group)>();

        foreach (var group in movieGroups)
        {
            var representative = group.Items[0];
            var movie = new Movie
            {
                Title = representative.Title,
                SortTitle = ResolveSortTitle(representative),
                ReleaseDate = representative.Year.HasValue ? new DateOnly(representative.Year.Value, 1, 1) : null
            };
            AddExternalIds(movie, representative.ExternalIds);
            context.Medias.Add(movie);
            pending.Add((movie, group));

            if (pending.Count >= SaveBatchSize)
            {
                await context.SaveChangesAsync(cancellationToken);
                newEnrichableMediaIds.AddRange(pending.Select(p => p.Entity.Id));
                FlushPending(pending, resultMap);
            }
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            newEnrichableMediaIds.AddRange(pending.Select(p => p.Entity.Id));
            FlushPending(pending, resultMap);
        }
    }

    private async Task CreateSeriesAsync(
        List<BatchGroup> batchGroups,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        List<Guid> newEnrichableMediaIds,
        CancellationToken cancellationToken)
    {
        var serieGroups = batchGroups.Where(g => g.MediaType == "serie").ToList();
        var pending = new List<(Serie Entity, BatchGroup Group)>();

        foreach (var group in serieGroups)
        {
            var representative = group.Items[0];
            var serie = new Serie
            {
                Title = representative.Title,
                SortTitle = ResolveSortTitle(representative),
                ReleaseDate = representative.Year.HasValue ? new DateOnly(representative.Year.Value, 1, 1) : null
            };
            AddExternalIds(serie, representative.ExternalIds);
            context.Medias.Add(serie);
            pending.Add((serie, group));

            if (pending.Count >= SaveBatchSize)
            {
                await context.SaveChangesAsync(cancellationToken);
                newEnrichableMediaIds.AddRange(pending.Select(p => p.Entity.Id));
                FlushPending(pending, resultMap);
            }
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            newEnrichableMediaIds.AddRange(pending.Select(p => p.Entity.Id));
            FlushPending(pending, resultMap);
        }
    }

    private async Task CreateMusicAsync(
        List<BatchGroup> batchGroups,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        List<Guid> newEnrichableMediaIds,
        CancellationToken cancellationToken)
    {
        var musicGroups = batchGroups.Where(g => g.MediaType == "music").ToList();
        if (musicGroups.Count == 0) return;

        // 1. Collect and create/find artists
        var artistNames = musicGroups
            .Select(g => g.Items[0].ArtistName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var artistCache = new Dictionary<string, Domain.Entities.Medias.MusicArtist>(StringComparer.OrdinalIgnoreCase);

        if (artistNames.Count > 0)
        {
            var artistNamesLower = artistNames.Select(n => n.ToLowerInvariant()).ToList();
            var existingArtists = await context.Medias.OfType<Domain.Entities.Medias.MusicArtist>()
                .Where(a => a.Title != null && artistNamesLower.Contains(a.Title.ToLower()))
                .ToListAsync(cancellationToken);

            foreach (var artist in existingArtists)
            {
                if (artist.Title is not null)
                    artistCache.TryAdd(artist.Title, artist);
            }

            var newArtists = new List<Domain.Entities.Medias.MusicArtist>();
            foreach (var name in artistNames)
            {
                if (!artistCache.ContainsKey(name))
                {
                    var artist = new Domain.Entities.Medias.MusicArtist
                    {
                        Title = name,
                        SortTitle = MediaSortTitleHelper.Compute(name)
                    };
                    context.Medias.Add(artist);
                    newArtists.Add(artist);
                    artistCache[name] = artist;
                }
            }

            if (newArtists.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        // 2. Collect and create/find albums
        var albumNames = musicGroups
            .Select(g => g.Items[0].AlbumName ?? "Unknown Album")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var albumNamesLower = albumNames.Select(n => n.ToLowerInvariant()).ToList();
        var existingAlbumsList = await context.Medias.OfType<MusicAlbum>()
            .Where(a => a.Title != null && albumNamesLower.Contains(a.Title.ToLower()))
            .ToListAsync(cancellationToken);

        var existingAlbumIds = existingAlbumsList.Select(a => a.Id).ToList();
        var albumsWithPlayableTracks = existingAlbumIds.Count == 0
            ? new HashSet<Guid>()
            : (await context.Medias.OfType<MusicTrack>()
                .Where(t => existingAlbumIds.Contains(t.AlbumId)
                    && t.IndexedFiles.Any())
                .Select(t => t.AlbumId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        // Load artist links for existing albums to match by artist+title
        var albumArtistLookup = existingAlbumsList
            .Where(a => a.ArtistId != null)
            .ToDictionary(a => a.Id, a => artistCache.Values.FirstOrDefault(ar => ar.Id == a.ArtistId)?.Title ?? "");

        var existingAlbums = new Dictionary<string, MusicAlbum>(StringComparer.OrdinalIgnoreCase);

        foreach (var album in existingAlbumsList)
        {
            // Never attach newly created (virtual) tracks onto albums that already have files.
            if (albumsWithPlayableTracks.Contains(album.Id))
                continue;

            var artistName = albumArtistLookup.GetValueOrDefault(album.Id, "");
            var key = MediaIdentityKeys.NormalizeKey(artistName, album.Title!);
            existingAlbums.TryAdd(key, album);
        }

        var albumCache = new Dictionary<string, MusicAlbum>(StringComparer.OrdinalIgnoreCase);
        var newAlbums = new List<MusicAlbum>();

        foreach (var group in musicGroups)
        {
            var albumName = group.Items[0].AlbumName ?? "Unknown Album";
            var albumKey = MediaIdentityKeys.NormalizeKey(group.Items[0].ArtistName ?? "", albumName);

            if (albumCache.ContainsKey(albumKey)) continue;

            if (existingAlbums.TryGetValue(albumKey, out var existing))
            {
                albumCache[albumKey] = existing;
            }
            else
            {
                // Keep virtual imports off playable albums: use a dedicated imported shell album.
                var playableConflict = existingAlbumsList.Any(a =>
                    string.Equals(a.Title, albumName, StringComparison.OrdinalIgnoreCase)
                    && albumsWithPlayableTracks.Contains(a.Id));
                var createTitle = playableConflict ? $"{albumName} (imported)" : albumName;

                var album = new MusicAlbum
                {
                    Title = createTitle,
                    SortTitle = MediaSortTitleHelper.Compute(createTitle)
                };
                context.Medias.Add(album);
                newAlbums.Add(album);
                albumCache[albumKey] = album;
            }
        }

        if (newAlbums.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            newEnrichableMediaIds.AddRange(newAlbums.Select(a => a.Id));
        }

        // 3. Link artists to albums via ArtistId
        foreach (var group in musicGroups)
        {
            var artistName = group.Items[0].ArtistName;
            var albumKey = MediaIdentityKeys.NormalizeKey(artistName ?? "", group.Items[0].AlbumName ?? "Unknown Album");

            if (artistName is null || !artistCache.TryGetValue(artistName, out var artist)) continue;
            if (!albumCache.TryGetValue(albumKey, out var album)) continue;
            if (album.ArtistId == artist.Id) continue;

            album.ArtistId = artist.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        // 4. Create tracks in batches and link artists
        var pending = new List<(MusicTrack Entity, BatchGroup Group)>();

        foreach (var group in musicGroups)
        {
            var representative = group.Items[0];
            var albumKey = MediaIdentityKeys.NormalizeKey(representative.ArtistName ?? "", representative.AlbumName ?? "Unknown Album");
            var album = albumCache[albumKey];
            var title = MediaIdentityKeys.StripTrackEditionSuffix(
                MediaIdentityKeys.StripRedundantArtistFromTitle(representative.Title, representative.ArtistName));

            var track = new MusicTrack
            {
                Title = title,
                SortTitle = MediaSortTitleHelper.Compute(title),
                AlbumId = album.Id
            };
            AddExternalIds(track, MusicExternalIdSets(group));
            context.Medias.Add(track);
            pending.Add((track, group));

            if (pending.Count >= SaveBatchSize)
            {
                LinkArtistsToTracks(pending, artistCache);
                await context.SaveChangesAsync(cancellationToken);
                FlushPending(pending, resultMap);
            }
        }

        if (pending.Count > 0)
        {
            LinkArtistsToTracks(pending, artistCache);
            await context.SaveChangesAsync(cancellationToken);
            FlushPending(pending, resultMap);
        }
    }

    private void LinkArtistsToTracks(
        List<(MusicTrack Entity, BatchGroup Group)> pending,
        Dictionary<string, Domain.Entities.Medias.MusicArtist> artistCache)
    {
        foreach (var (track, group) in pending)
        {
            var artistName = group.Items[0].ArtistName;
            if (artistName is null || !artistCache.TryGetValue(artistName, out var artist)) continue;

            track.ArtistId = artist.Id;
        }
    }

    private async Task CreateEpisodesAsync(
        List<BatchGroup> batchGroups,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        List<Guid> newEnrichableMediaIds,
        CancellationToken cancellationToken)
    {
        var episodeGroups = batchGroups.Where(g => g.MediaType == "episode").ToList();
        if (episodeGroups.Count == 0) return;

        // Batch-create/find all series first
        var seriesTitles = episodeGroups
            .Select(g => g.Items[0].SeriesTitle ?? "Unknown Series")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingSeriesList = await context.Medias.OfType<Serie>().ToListAsync(cancellationToken);

        var existingSeriesByExactTitle = existingSeriesList
            .Where(s => s.Title is not null)
            .GroupBy(s => s.Title!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var serieCache = new Dictionary<string, Serie>(StringComparer.OrdinalIgnoreCase);
        var newSeries = new List<Serie>();

        foreach (var title in seriesTitles)
        {
            var identityHits = MediaIdentityKeys.ResolveSeriesMatches(
                title, existingSeriesList, s => s.Title, s => s.OriginalTitle);
            if (identityHits.Count == 1)
            {
                serieCache[title] = identityHits[0];
                continue;
            }

            if (existingSeriesByExactTitle.TryGetValue(title, out var existing))
            {
                serieCache[title] = existing;
                continue;
            }

            var representative = episodeGroups
                .First(g => string.Equals(g.Items[0].SeriesTitle ?? "Unknown Series", title, StringComparison.OrdinalIgnoreCase))
                .Items[0];
            var serie = new Serie
            {
                Title = title,
                SortTitle = MediaSortTitleHelper.Compute(title),
                ReleaseDate = representative.Year is { } y ? new DateOnly(y, 1, 1) : null
            };
            context.Medias.Add(serie);
            newSeries.Add(serie);
            serieCache[title] = serie;
        }

        if (newSeries.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            newEnrichableMediaIds.AddRange(newSeries.Select(s => s.Id));
        }

        // Batch-create/find all seasons
        var seasonKeys = episodeGroups
            .Select(g => (SeriesTitle: g.Items[0].SeriesTitle ?? "Unknown Series", SeasonNumber: g.Items[0].SeasonNumber ?? 1))
            .Distinct()
            .ToList();

        var serieIds = serieCache.Values.Select(s => s.Id).ToList();
        var existingSeasons = await context.Medias.OfType<SerieSeason>()
            .Where(s => serieIds.Contains(s.SerieId))
            .ToListAsync(cancellationToken);

        var seasonCache = new Dictionary<string, SerieSeason>(StringComparer.OrdinalIgnoreCase);
        var newSeasons = new List<SerieSeason>();

        foreach (var (seriesTitle, seasonNumber) in seasonKeys)
        {
            var cacheKey = $"{seriesTitle}|S{seasonNumber}";
            var serie = serieCache[seriesTitle];
            var existing = existingSeasons.FirstOrDefault(s => s.SerieId == serie.Id && s.SeasonNumber == seasonNumber);

            if (existing is not null)
            {
                seasonCache[cacheKey] = existing;
            }
            else
            {
                var seasonTitle = $"Season {seasonNumber}";
                var season = new SerieSeason
                {
                    Title = seasonTitle,
                    SortTitle = MediaSortTitleHelper.Compute(seasonTitle),
                    SerieId = serie.Id,
                    SeasonNumber = seasonNumber
                };
                context.Medias.Add(season);
                newSeasons.Add(season);
                seasonCache[cacheKey] = season;
            }
        }

        if (newSeasons.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Now batch-create episodes
        var pending = new List<(SerieEpisode Entity, BatchGroup Group)>();

        foreach (var group in episodeGroups)
        {
            var representative = group.Items[0];
            var seriesTitle = representative.SeriesTitle ?? "Unknown Series";
            var seasonNumber = representative.SeasonNumber ?? 1;
            var seasonKey = $"{seriesTitle}|S{seasonNumber}";

            var episode = new SerieEpisode
            {
                Title = representative.Title,
                SortTitle = ResolveSortTitle(representative),
                SerieId = serieCache[seriesTitle].Id,
                SeasonId = seasonCache[seasonKey].Id,
                EpisodeNumber = representative.EpisodeNumber ?? 0
            };
            AddExternalIds(episode, representative.ExternalIds);
            context.Medias.Add(episode);
            pending.Add((episode, group));

            if (pending.Count >= SaveBatchSize)
            {
                await context.SaveChangesAsync(cancellationToken);
                FlushPending(pending, resultMap);
            }
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            FlushPending(pending, resultMap);
        }
    }

    private async Task QueueMetadataRefreshAsync(List<Guid> mediaIds, CancellationToken cancellationToken)
    {
        var supportedProviderNames = metadataProviders.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enrichableMedia = await context.Medias
            .Include(m => m.ExternalIds)
            .Where(m => mediaIds.Contains(m.Id))
            .Where(m => m is Movie || m is MusicAlbum || m is Serie)
            .Where(m => m.ExternalIds.Count > 0)
            .ToListAsync(cancellationToken);

        foreach (var media in enrichableMedia)
        {
            var externalId = media.ExternalIds.FirstOrDefault(e => supportedProviderNames.Contains(e.ProviderName));
            if (externalId is null) continue;

            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = media.Id,
                    MetadataProviderExternalId = externalId.Value,
                    MetadataProviderName = externalId.ProviderName,
                    Language = "fr",
                    FallbackLanguage = "en"
                },
                TargetEntityId = media.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                Lane = BackgroundTaskLane.Metadata,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(externalId.ProviderName),
                WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
                TriggeredBy = BackgroundTaskTriggeredBy.User,
                MaxAttempts = 3
            }, cancellationToken);
        }
    }

    private static void FlushPending<T>(
        List<(T Entity, BatchGroup Group)> pending,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap) where T : BaseMedia
    {
        foreach (var (entity, group) in pending)
        {
            foreach (var item in group.Items)
            {
                resultMap.TryAdd(item.Key, (entity.Id, true));
            }
        }
        pending.Clear();
    }

    private static void AddExternalIds(BaseMedia media, Dictionary<string, string> externalIds) =>
        AddExternalIds(media, [externalIds]);

    private static void AddExternalIds(BaseMedia media, IEnumerable<Dictionary<string, string>> idSets)
    {
        foreach (var externalIds in idSets)
        {
            foreach (var (provider, value) in externalIds)
            {
                if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(value))
                    continue;

                if (media.ExternalIds.Any(e =>
                        string.Equals(e.ProviderName, provider, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase)))
                    continue;

                media.ExternalIds.Add(new ExternalId { ProviderName = provider, Value = value });
            }
        }
    }

    private static List<BatchGroup> GroupForIntraBatchDedup(List<BulkCreateMediasRequest.BulkCreateMediaItem> items)
    {
        var groups = new List<BatchGroup>();
        var assigned = new HashSet<string>();

        foreach (var item in items)
        {
            if (assigned.Contains(item.Key)) continue;

            var group = new BatchGroup
            {
                MediaType = item.MediaType,
                Items = [item]
            };

            // Find duplicates in the batch by matching ExternalIds
            foreach (var other in items.Where(o => o.Key != item.Key && !assigned.Contains(o.Key) && o.MediaType == item.MediaType))
            {
                var grouped = false;
                if (item.ExternalIds.Count > 0 && other.ExternalIds.Count > 0)
                {
                    var hasCommon = item.ExternalIds.Any(e =>
                        other.ExternalIds.TryGetValue(e.Key, out var v) &&
                        string.Equals(v, e.Value, StringComparison.OrdinalIgnoreCase));

                    if (hasCommon)
                        grouped = true;
                }

                if (!grouped && item.MediaType == "music" &&
                    string.Equals(MediaIdentityKeys.NormalizeMusicTitle(item.ArtistName, item.Title),
                                  MediaIdentityKeys.NormalizeMusicTitle(other.ArtistName, other.Title),
                                  StringComparison.OrdinalIgnoreCase))
                {
                    grouped = true;
                }
                else if (!grouped && item.MediaType is "movie" or "serie" &&
                         string.Equals(MediaIdentityKeys.NormalizeMovieTitle(item.Title, item.Year),
                                       MediaIdentityKeys.NormalizeMovieTitle(other.Title, other.Year),
                                       StringComparison.OrdinalIgnoreCase))
                {
                    grouped = true;
                }

                if (!grouped)
                    continue;

                group.Items.Add(other);
                assigned.Add(other.Key);
            }

            assigned.Add(item.Key);
            groups.Add(group);
        }

        foreach (var group in groups.Where(g => g.MediaType == "music" && g.Items.Count > 1))
        {
            group.Items.Sort(CompareMusicRepresentative);
        }

        return groups;
    }

    private static int CompareMusicRepresentative(
        BulkCreateMediasRequest.BulkCreateMediaItem left,
        BulkCreateMediasRequest.BulkCreateMediaItem right)
    {
        var popularity = (right.Popularity ?? int.MinValue).CompareTo(left.Popularity ?? int.MinValue);
        if (popularity != 0)
            return popularity;

        return string.Compare(left.Key, right.Key, StringComparison.Ordinal);
    }

    private static IEnumerable<KeyValuePair<string, string>> OrderedExternalIds(
        BulkCreateMediasRequest.BulkCreateMediaItem item)
    {
        // Prefer K7 ISRC / MusicBrainz hits over a leftover Spotify id on a virtual track.
        string[] priority = ["isrc", "musicbrainz"];
        foreach (var provider in priority)
        {
            if (item.ExternalIds.TryGetValue(provider, out var value))
                yield return new KeyValuePair<string, string>(provider, value);
        }

        foreach (var pair in item.ExternalIds)
        {
            if (priority.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                continue;

            yield return pair;
        }
    }

    private async Task PropagateMusicGroupMatchesAsync(
        IReadOnlyList<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        CancellationToken cancellationToken)
    {
        var music = items.Where(i => i.MediaType == "music").ToList();
        if (music.Count == 0)
            return;

        foreach (var group in music.GroupBy(
                     i => MediaIdentityKeys.NormalizeMusicTitle(i.ArtistName, i.Title),
                     StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToList();
            var hitMembers = members.Where(i => resultMap.ContainsKey(i.Key)).ToList();
            if (hitMembers.Count == 0)
                continue;

            var mediaIds = hitMembers.Select(i => resultMap[i.Key].MediaId).Distinct().ToList();
            var playableIds = (await context.Medias
                    .Where(m => mediaIds.Contains(m.Id) && m.IndexedFiles.Any())
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var chosen = PickGroupMediaId(hitMembers, resultMap, playableIds);
            foreach (var item in members)
                resultMap.TryAdd(item.Key, (chosen, false));
        }
    }

    private static Guid PickGroupMediaId(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> hitMembers,
        Dictionary<string, (Guid MediaId, bool WasCreated)> resultMap,
        HashSet<Guid> playableIds)
    {
        var isrcPlayable = hitMembers.FirstOrDefault(i =>
            HasIsrc(i) && playableIds.Contains(resultMap[i.Key].MediaId));
        if (isrcPlayable is not null)
            return resultMap[isrcPlayable.Key].MediaId;

        var playable = hitMembers.FirstOrDefault(i => playableIds.Contains(resultMap[i.Key].MediaId));
        if (playable is not null)
            return resultMap[playable.Key].MediaId;

        var isrcVirtual = hitMembers.FirstOrDefault(HasIsrc);
        if (isrcVirtual is not null)
            return resultMap[isrcVirtual.Key].MediaId;

        return resultMap[hitMembers[0].Key].MediaId;
    }

    private static bool HasIsrc(BulkCreateMediasRequest.BulkCreateMediaItem item) =>
        item.ExternalIds.Keys.Any(k => k.Equals("isrc", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Dictionary<string, string>> MusicExternalIdSets(BatchGroup group)
    {
        for (var i = 0; i < group.Items.Count; i++)
        {
            var item = group.Items[i];
            var ids = new Dictionary<string, string>(item.ExternalIds, StringComparer.OrdinalIgnoreCase);
            if (i > 0)
                ids.Remove("isrc");

            yield return ids;

            foreach (var spotifyId in item.AdditionalSpotifyIds ?? [])
            {
                if (string.IsNullOrWhiteSpace(spotifyId))
                    continue;

                yield return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["spotify"] = spotifyId
                };
            }
        }
    }

    private static string? ResolveSortTitle(BulkCreateMediasRequest.BulkCreateMediaItem item) =>
        item.SortTitle ?? MediaSortTitleHelper.Compute(item.Title);

    private sealed class BatchGroup
    {
        public required string MediaType { get; init; }
        public List<BulkCreateMediasRequest.BulkCreateMediaItem> Items { get; init; } = [];
    }
}
