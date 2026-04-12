using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Commands.BulkCreateMedias;

[Authorize(Roles = Roles.Administrator)]
public record BulkCreateMediasCommand : IRequest<BulkCreateMediasResponse>
{
    public required IReadOnlyList<BulkCreateMediasRequest.BulkCreateMediaItem> Items { get; init; }
    public bool FetchMetadata { get; init; }
}

public partial class BulkCreateMediasCommandHandler(IApplicationDbContext context, ISender sender, IEnumerable<IMetadataProviderInfo> metadataProviders)
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

        var externalIdLookup = await LookupByExternalIdsAsync(itemsWithExternalIds, cancellationToken);

        foreach (var item in itemsWithExternalIds)
        {
            foreach (var (provider, value) in item.ExternalIds)
            {
                if (externalIdLookup.TryGetValue((provider, value), out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                    break;
                }
            }
        }

        // 2. Title-based dedup for music items without ExternalId match
        var unmatchedMusic = request.Items
            .Where(i => i.MediaType == "music" && !resultMap.ContainsKey(i.Key))
            .ToList();

        if (unmatchedMusic.Count > 0)
        {
            var titleLookup = await LookupMusicByTitleAsync(unmatchedMusic, cancellationToken);
            foreach (var item in unmatchedMusic)
            {
                var titleKey = NormalizeMusicTitle(item.ArtistName, item.Title);
                if (titleLookup.TryGetValue(titleKey, out var mediaId))
                {
                    resultMap.TryAdd(item.Key, (mediaId, false));
                }
            }
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

        var artistCache = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);

        if (artistNames.Count > 0)
        {
            var artistNamesLower = artistNames.Select(n => n.ToLowerInvariant()).ToList();
            var existingArtists = await context.Persons
                .Where(p => p.Name != null && artistNamesLower.Contains(p.Name.ToLower()))
                .ToListAsync(cancellationToken);

            foreach (var person in existingArtists)
            {
                artistCache.TryAdd(person.Name, person);
            }

            var newArtists = new List<Person>();
            foreach (var name in artistNames)
            {
                if (!artistCache.ContainsKey(name))
                {
                    var person = new Person { Name = name };
                    context.Persons.Add(person);
                    newArtists.Add(person);
                    artistCache[name] = person;
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

        // Load artist links for existing albums to match by artist+title
        var existingAlbumIds = existingAlbumsList.Select(a => a.Id).ToList();
        var albumArtistLinks = existingAlbumIds.Count > 0
            ? await context.PersonRoles.OfType<MusicArtist>()
                .Where(r => existingAlbumIds.Contains(r.MediaId))
                .Select(r => new { r.MediaId, ArtistName = r.Person.Name })
                .ToListAsync(cancellationToken)
            : [];

        var albumArtistLookup = albumArtistLinks
            .GroupBy(r => r.MediaId)
            .ToDictionary(g => g.Key, g => g.First().ArtistName);

        var existingAlbums = new Dictionary<string, MusicAlbum>(StringComparer.OrdinalIgnoreCase);

        foreach (var album in existingAlbumsList)
        {
            var artistName = albumArtistLookup.GetValueOrDefault(album.Id, "");
            var key = NormalizeKey(artistName ?? "", album.Title!);
            existingAlbums.TryAdd(key, album);
        }

        var albumCache = new Dictionary<string, MusicAlbum>(StringComparer.OrdinalIgnoreCase);
        var newAlbums = new List<MusicAlbum>();

        foreach (var group in musicGroups)
        {
            var albumName = group.Items[0].AlbumName ?? "Unknown Album";
            var albumKey = NormalizeKey(group.Items[0].ArtistName ?? "", albumName);

            if (albumCache.ContainsKey(albumKey)) continue;

            if (existingAlbums.TryGetValue(albumKey, out var existing))
            {
                albumCache[albumKey] = existing;
            }
            else
            {
                var album = new MusicAlbum { Title = albumName };
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

        // 3. Link artists to albums via MusicArtist roles
        var existingAlbumRoles = await context.PersonRoles
            .OfType<MusicArtist>()
            .Where(r => albumCache.Values.Select(a => a.Id).Contains(r.MediaId))
            .Select(r => new { r.PersonId, r.MediaId })
            .ToListAsync(cancellationToken);

        var existingAlbumRoleKeys = existingAlbumRoles
            .Select(r => (r.PersonId, r.MediaId))
            .ToHashSet();

        var newAlbumRoles = new List<MusicArtist>();
        foreach (var group in musicGroups)
        {
            var artistName = group.Items[0].ArtistName;
            var albumKey = NormalizeKey(artistName ?? "", group.Items[0].AlbumName ?? "Unknown Album");

            if (artistName is null || !artistCache.TryGetValue(artistName, out var person)) continue;
            if (!albumCache.TryGetValue(albumKey, out var album)) continue;
            if (existingAlbumRoleKeys.Contains((person.Id, album.Id))) continue;

            existingAlbumRoleKeys.Add((person.Id, album.Id));
            newAlbumRoles.Add(new MusicArtist
            {
                PersonId = person.Id,
                MediaId = album.Id,
                IsGuest = false
            });
        }

        if (newAlbumRoles.Count > 0)
        {
            context.PersonRoles.AddRange(newAlbumRoles);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 4. Create tracks in batches and link artists
        var pending = new List<(MusicTrack Entity, BatchGroup Group)>();

        foreach (var group in musicGroups)
        {
            var representative = group.Items[0];
            var albumKey = NormalizeKey(representative.ArtistName ?? "", representative.AlbumName ?? "Unknown Album");
            var album = albumCache[albumKey];

            var track = new MusicTrack
            {
                Title = representative.Title,
                AlbumId = album.Id
            };
            AddExternalIds(track, representative.ExternalIds);
            context.Medias.Add(track);
            pending.Add((track, group));

            if (pending.Count >= SaveBatchSize)
            {
                await context.SaveChangesAsync(cancellationToken);
                LinkArtistsToTracks(pending, artistCache);
                await context.SaveChangesAsync(cancellationToken);
                FlushPending(pending, resultMap);
            }
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            LinkArtistsToTracks(pending, artistCache);
            await context.SaveChangesAsync(cancellationToken);
            FlushPending(pending, resultMap);
        }
    }

    private void LinkArtistsToTracks(
        List<(MusicTrack Entity, BatchGroup Group)> pending,
        Dictionary<string, Person> artistCache)
    {
        foreach (var (track, group) in pending)
        {
            var artistName = group.Items[0].ArtistName;
            if (artistName is null || !artistCache.TryGetValue(artistName, out var person)) continue;

            context.PersonRoles.Add(new MusicArtist
            {
                PersonId = person.Id,
                MediaId = track.Id,
                IsGuest = false
            });
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

        var existingSeriesList = await context.Medias.OfType<Serie>()
            .Where(s => s.Title != null && seriesTitles.Contains(s.Title))
            .ToListAsync(cancellationToken);

        var existingSeries = existingSeriesList
            .GroupBy(s => s.Title!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var serieCache = new Dictionary<string, Serie>(StringComparer.OrdinalIgnoreCase);
        var newSeries = new List<Serie>();

        foreach (var title in seriesTitles)
        {
            if (existingSeries.TryGetValue(title, out var existing))
            {
                serieCache[title] = existing;
            }
            else
            {
                var representative = episodeGroups
                    .First(g => string.Equals(g.Items[0].SeriesTitle ?? "Unknown Series", title, StringComparison.OrdinalIgnoreCase))
                    .Items[0];
                var serie = new Serie
                {
                    Title = title,
                    ReleaseDate = representative.Year is { } y ? new DateOnly(y, 1, 1) : null
                };
                context.Medias.Add(serie);
                newSeries.Add(serie);
                serieCache[title] = serie;
            }
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
                var season = new SerieSeason
                {
                    Title = $"Season {seasonNumber}",
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
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = media.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 1,
                ConcurrencyGroup = externalId.ProviderName
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

    private async Task<Dictionary<(string Provider, string Value), Guid>> LookupByExternalIdsAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken)
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
                    Expression.Property(parameter, nameof(ExternalId.ProviderName)),
                    Expression.Constant(provider));
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
                .Select(e => new { e.ProviderName, e.Value, e.MediaId })
                .ToListAsync(cancellationToken);

            foreach (var match in matches)
            {
                if (match.MediaId.HasValue)
                {
                    result.TryAdd((match.ProviderName, match.Value), match.MediaId.Value);
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<string, Guid>> LookupMusicByTitleAsync(
        List<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var trackTitles = items
            .Select(i => i.Title)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var strippedTitles = trackTitles
            .Select(StripFeatureCredits)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allTitles = trackTitles.Concat(strippedTitles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allTitles.Count == 0) return result;

        var allTitlesLower = allTitles.Select(t => t.ToLowerInvariant()).ToList();

        var tracks = await context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.Title != null && allTitlesLower.Contains(t.Title.ToLower()))
            .Where(t => t.IndexedFiles.Any())
            .Select(t => new
            {
                t.Id,
                t.Title,
                AlbumTitle = t.Album != null ? t.Album.Title : null,
                ArtistName = t.PersonRoles.OfType<MusicArtist>().Select(r => r.Person.Name).FirstOrDefault()
                          ?? (t.Album != null ? t.Album.PersonRoles.OfType<MusicArtist>().Select(r => r.Person.Name).FirstOrDefault() : null)
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = NormalizeMusicTitle(item.ArtistName, item.Title);
            if (result.ContainsKey(key)) continue;

            var itemTitleStripped = StripFeatureCredits(item.Title);

            var match = tracks.FirstOrDefault(t =>
            {
                var dbTitleStripped = StripFeatureCredits(t.Title!);
                var titleMatch = string.Equals(t.Title, item.Title, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(dbTitleStripped, itemTitleStripped, StringComparison.OrdinalIgnoreCase);
                if (!titleMatch) return false;

                return string.Equals(t.ArtistName, item.ArtistName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.AlbumTitle, item.AlbumName, StringComparison.OrdinalIgnoreCase);
            });

            if (match is not null)
            {
                result.TryAdd(key, match.Id);
            }
        }

        return result;
    }

    private static void AddExternalIds(BaseMedia media, Dictionary<string, string> externalIds)
    {
        foreach (var (provider, value) in externalIds)
        {
            media.ExternalIds.Add(new ExternalId { ProviderName = provider, Value = value });
        }
    }

    private static string NormalizeMusicTitle(string? artistName, string title)
    {
        return artistName is not null ? $"{artistName} - {title}" : title;
    }

    private static partial class TrackTitleRegex
    {
        [GeneratedRegex(@"\s*[\(\[](feat\.?|ft\.?|with)\s.+?[\)\]]", RegexOptions.IgnoreCase)]
        public static partial Regex FeatureCredits();
    }

    private static string StripFeatureCredits(string title)
    {
        return TrackTitleRegex.FeatureCredits().Replace(title, "").Trim();
    }

    private static string NormalizeKey(string part1, string part2)
    {
        return $"{part1.ToUpperInvariant()}|{part2.ToUpperInvariant()}";
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
                if (item.ExternalIds.Count > 0 && other.ExternalIds.Count > 0)
                {
                    var hasCommon = item.ExternalIds.Any(e =>
                        other.ExternalIds.TryGetValue(e.Key, out var v) &&
                        string.Equals(v, e.Value, StringComparison.OrdinalIgnoreCase));

                    if (hasCommon)
                    {
                        group.Items.Add(other);
                        assigned.Add(other.Key);
                    }
                }
                else if (item.MediaType == "music" &&
                         string.Equals(NormalizeMusicTitle(item.ArtistName, item.Title),
                                       NormalizeMusicTitle(other.ArtistName, other.Title),
                                       StringComparison.OrdinalIgnoreCase))
                {
                    group.Items.Add(other);
                    assigned.Add(other.Key);
                }
            }

            assigned.Add(item.Key);
            groups.Add(group);
        }

        return groups;
    }

    private sealed class BatchGroup
    {
        public required string MediaType { get; init; }
        public List<BulkCreateMediasRequest.BulkCreateMediaItem> Items { get; init; } = [];
    }
}
