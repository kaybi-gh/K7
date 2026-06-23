using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public sealed class MediaBrowseService : IMediaBrowseService
{
    private const string RootHome = "root:home";
    private const string RootRecentLegacy = "root:recent";
    private const string RootArtists = "root:artists";
    private const string RootPlaylists = "root:playlists";
    private const string RootDownloads = "root:downloads";
    private const string HomeSectionRecentPlaylists = "home-recent-playlists";
    private const string HomeSectionRecentlyAdded = "home-recently-added";
    private const string HomeSectionRecentPlays = "home-recent-plays";
    private const string HomeSectionRecentPlaylistsLegacy = "home:section:recent-playlists";
    private const string HomeSectionRecentlyAddedLegacy = "home:section:recently-added";
    private const string HomeSectionRecentPlaysLegacy = "home:section:recent-plays";

    private const string PrefixAlbum = "album:";
    private const string PrefixArtist = "artist:";
    private const string PrefixPlaylist = "playlist:";
    private const string PrefixTrack = "track:";
    private const string PrefixDownloadGroup = "download-group:";
    private const string PrefixArtistLetter = "artists-letter:";
    private const string ShuffleSuffix = ":shuffle";

    private readonly IMediaService _mediaService;
    private readonly IPlaylistService _playlistService;
    private readonly IServerInfoService _serverInfoService;
    private readonly IK7ServerService _apiClient;
    private readonly IOfflineMediaStore _offlineStore;

    public MediaBrowseService(
        IMediaService mediaService,
        IPlaylistService playlistService,
        IServerInfoService serverInfoService,
        IK7ServerService apiClient,
        IOfflineMediaStore offlineStore)
    {
        _mediaService = mediaService;
        _playlistService = playlistService;
        _serverInfoService = serverInfoService;
        _apiClient = apiClient;
        _offlineStore = offlineStore;
    }

    public Task<IReadOnlyList<MediaBrowseItem>> GetRootItemsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MediaBrowseItem> items =
        [
            new MediaBrowseItem { Id = RootHome, Title = "Home", IsBrowsable = true },
            new MediaBrowseItem { Id = RootArtists, Title = "Library", IsBrowsable = true },
            new MediaBrowseItem { Id = RootPlaylists, Title = "Playlists", IsBrowsable = true },
            new MediaBrowseItem { Id = RootDownloads, Title = "Downloads", IsBrowsable = true }
        ];
        return Task.FromResult(items);
    }

    public async Task<IReadOnlyList<MediaBrowseItem>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return parentId switch
        {
            RootHome => await GetHomeItemsAsync(cancellationToken),
            RootRecentLegacy => await GetRecentlyPlayedAsync(cancellationToken),
            RootDownloads => await GetDownloadGroupsWithShuffleAsync(cancellationToken),
            RootArtists => GetAlphabetIndex(PrefixArtistLetter),
            RootPlaylists => await GetPlaylistsAsync(cancellationToken),
            HomeSectionRecentPlaylists or HomeSectionRecentPlaylistsLegacy => await GetHomeRecentPlaylistsAsync(cancellationToken),
            HomeSectionRecentlyAdded or HomeSectionRecentlyAddedLegacy => await GetHomeRecentlyAddedAsync(cancellationToken),
            HomeSectionRecentPlays or HomeSectionRecentPlaysLegacy => await GetRecentlyPlayedAsync(cancellationToken),
            _ when parentId.StartsWith(PrefixArtistLetter) => await GetArtistsByLetterAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixDownloadGroup) => await GetDownloadGroupTracksAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixArtist) => await GetArtistAlbumsAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixAlbum) => await GetAlbumTracksAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixPlaylist) => await GetPlaylistTracksAsync(parentId, cancellationToken),
            _ => []
        };
    }

    public async Task<IReadOnlyList<AudioQueueItem>> GetPlayableItemsAsync(string parentId, CancellationToken cancellationToken = default)
    {
        // Shuffle must be checked first (e.g. "artist:guid:shuffle" would match PrefixArtist otherwise)
        if (parentId.EndsWith(ShuffleSuffix))
        {
            var baseId = parentId[..^ShuffleSuffix.Length];
            var queue = await GetPlayableItemsAsync(baseId, cancellationToken);
            return Shuffle(queue);
        }

        // Handle individual track IDs: "album:albumGuid:trackGuid", "playlist:playlistGuid:mediaGuid", "download-group:name:mediaGuid"
        if (parentId.StartsWith(PrefixAlbum))
        {
            var suffix = parentId[PrefixAlbum.Length..];
            var colonIdx = suffix.IndexOf(':');
            if (colonIdx > 0 && Guid.TryParse(suffix[..colonIdx], out _))
            {
                // It's a track within an album - load the album queue and find start index
                var albumId = $"{PrefixAlbum}{suffix[..colonIdx]}";
                var queue = await GetAlbumQueueAsync(albumId, cancellationToken);
                if (Guid.TryParse(suffix[(colonIdx + 1)..], out var trackId))
                {
                    var startIdx = queue.ToList().FindIndex(q => q.MediaId == trackId);
                    if (startIdx > 0)
                        return queue.Skip(startIdx).Concat(queue.Take(startIdx)).ToArray();
                }
                return queue;
            }
            return await GetAlbumQueueAsync(parentId, cancellationToken);
        }

        if (parentId.StartsWith(PrefixPlaylist))
        {
            var suffix = parentId[PrefixPlaylist.Length..];
            var colonIdx = suffix.IndexOf(':');
            if (colonIdx > 0 && Guid.TryParse(suffix[..colonIdx], out _))
            {
                var playlistId = $"{PrefixPlaylist}{suffix[..colonIdx]}";
                var queue = await GetPlaylistQueueAsync(playlistId, cancellationToken);
                if (Guid.TryParse(suffix[(colonIdx + 1)..], out var trackId))
                {
                    var startIdx = queue.ToList().FindIndex(q => q.MediaId == trackId);
                    if (startIdx > 0)
                        return queue.Skip(startIdx).Concat(queue.Take(startIdx)).ToArray();
                }
                return queue;
            }
            return await GetPlaylistQueueAsync(parentId, cancellationToken);
        }

        if (parentId.StartsWith(PrefixDownloadGroup))
        {
            return await GetDownloadGroupQueueAsync(parentId, cancellationToken);
        }

        if (parentId.StartsWith(PrefixTrack))
        {
            return await GetSingleTrackQueueAsync(parentId, cancellationToken);
        }

        if (parentId.StartsWith(PrefixArtist))
        {
            return await GetArtistQueueAsync(parentId, cancellationToken);
        }

        return parentId switch
        {
            RootDownloads => await GetDownloadQueueAsync(cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetRecentlyPlayedAsync(CancellationToken cancellationToken)
    {
        var history = await _serverInfoService.GetPlaybackHistoryAsync(page: 1, pageSize: 30, mediaType: "MusicTrack", cancellationToken: cancellationToken);

        if (history is null || history.Items.Count == 0)
            return [];

        // Deduplicate by MediaId, keep most recent
        return history.Items
            .GroupBy(h => h.MediaId)
            .Select(g => g.First())
            .Select(h => new MediaBrowseItem
            {
                Id = $"{PrefixTrack}{h.MediaId}",
                Title = h.MediaTitle ?? "Unknown",
                ArtworkUrl = h.ImageUrl is not null ? _apiClient.GetAbsoluteUri(h.ImageUrl)?.AbsoluteUri : null,
                IsPlayable = true
            })
            .ToArray();
    }

    private static IReadOnlyList<MediaBrowseItem> GetAlphabetIndex(string prefix)
    {
        var letters = new List<MediaBrowseItem>();
        letters.Add(new MediaBrowseItem { Id = $"{prefix}#", Title = "#", IsBrowsable = true });
        for (var c = 'A'; c <= 'Z'; c++)
            letters.Add(new MediaBrowseItem { Id = $"{prefix}{c}", Title = c.ToString(), IsBrowsable = true });
        return letters;
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetArtistsByLetterAsync(string parentId, CancellationToken cancellationToken)
    {
        var letter = parentId[PrefixArtistLetter.Length..];
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicArtist],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 5000
        }, cancellationToken);

        var items = (result?.Items ?? []).OfType<LiteMusicArtistDto>();

        items = letter == "#"
            ? items.Where(a => a.Title is null || (a.Title.Length > 0 && !char.IsLetter(a.Title[0])))
            : items.Where(a => a.Title is not null && a.Title.StartsWith(letter, StringComparison.OrdinalIgnoreCase));

        return items
            .Select(a => new MediaBrowseItem
            {
                Id = $"{PrefixArtist}{a.Id}",
                Title = a.Title ?? "Unknown Artist",
                ArtworkUrl = GetPictureUrl(a.Pictures),
                IsBrowsable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetPlaylistsAsync(CancellationToken cancellationToken)
    {
        var result = await _playlistService.GetPlaylistsAsync(
            pageNumber: 1,
            pageSize: 100,
            cancellationToken: cancellationToken);

        return (result?.Items ?? [])
            .Where(p => p.MediaType is not (MediaType.Movie or MediaType.Serie or MediaType.SerieEpisode or MediaType.SerieSeason))
            .Select(p => new MediaBrowseItem
            {
                Id = $"{PrefixPlaylist}{p.Id}",
                Title = p.Title,
                Subtitle = $"{p.ItemCount} tracks",
                ArtworkUrl = GetPictureUrl(p.CoverPicture),
                IsBrowsable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetArtistAlbumsAsync(string parentId, CancellationToken cancellationToken)
    {
        var artistId = Guid.Parse(parentId[PrefixArtist.Length..]);
        var media = await _mediaService.GetMediaAsync(artistId, cancellationToken);

        if (media is not MusicArtistDto artist)
            return [];

        var items = new List<MediaBrowseItem>
        {
            new() { Id = $"{parentId}{ShuffleSuffix}", Title = "Shuffle All", IsPlayable = true }
        };

        items.AddRange((artist.Albums ?? [])
            .Select(a => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{a.Id}",
                Title = a.Title ?? "Unknown Album",
                Subtitle = a.ReleaseDate?.Year.ToString(),
                ArtworkUrl = GetPictureUrl(a.Pictures),
                IsBrowsable = true
            }));

        return items;
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetAlbumTracksAsync(string parentId, CancellationToken cancellationToken)
    {
        var albumId = Guid.Parse(parentId[PrefixAlbum.Length..]);
        var media = await _mediaService.GetMediaAsync(albumId, cancellationToken);

        if (media is not MusicAlbumDto album)
            return [];

        var coverUrl = GetPictureUrl(album.Pictures);

        var items = new List<MediaBrowseItem>
        {
            new() { Id = parentId, Title = "Play All", IsPlayable = true },
            new() { Id = $"{parentId}{ShuffleSuffix}", Title = "Shuffle", IsPlayable = true }
        };

        items.AddRange((album.Tracks ?? [])
            .OrderBy(t => t.TrackNumber)
            .Select(t => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{albumId}:{t.Id}",
                Title = t.Title ?? "Unknown Track",
                Subtitle = album.ArtistName,
                ArtworkUrl = (t.Pictures != null && t.Pictures.Count > 0) ? GetPictureUrl(t.Pictures) : coverUrl,
                IsPlayable = true
            }));

        return items;

    }

    // Nouvelle page Home : playlists récentes, albums récents, morceaux récemment joués
    private async Task<IReadOnlyList<MediaBrowseItem>> GetHomeItemsAsync(CancellationToken cancellationToken)
    {
        var homeSections = new List<MediaBrowseItem>
        {
            new() { Id = HomeSectionRecentPlaylists, Title = "Recent Playlists", IsBrowsable = true },
            new() { Id = HomeSectionRecentPlays, Title = "Recent Plays", IsBrowsable = true },
            new() { Id = HomeSectionRecentlyAdded, Title = "Recently Added", IsBrowsable = true }
        };

        var hasAny = (await GetHomeRecentPlaylistsAsync(cancellationToken)).Count > 0
            || (await GetRecentlyPlayedAsync(cancellationToken)).Count > 0
            || (await GetHomeRecentlyAddedAsync(cancellationToken)).Count > 0;

        if (!hasAny)
        {
            return
            [
                new MediaBrowseItem
                {
                    Id = "home:server-unreachable",
                    Title = "Server unavailable",
                    Subtitle = "You can still use Downloads",
                    IsBrowsable = false,
                    IsPlayable = false
                }
            ];
        }

        return homeSections;
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetHomeRecentPlaylistsAsync(CancellationToken cancellationToken)
    {
        var playlists = await _playlistService.GetPlaylistsAsync(pageNumber: 1, pageSize: 30, cancellationToken: cancellationToken);

        return (playlists?.Items ?? [])
            .Where(p => p.MediaType is not (MediaType.Movie or MediaType.Serie or MediaType.SerieEpisode or MediaType.SerieSeason))
            .OrderByDescending(p => p.LastModified)
            .Take(30)
            .Select(p => new MediaBrowseItem
            {
                Id = $"{PrefixPlaylist}{p.Id}",
                Title = p.Title,
                Subtitle = $"{p.ItemCount} tracks",
                ArtworkUrl = GetPictureUrl(p.CoverPicture),
                IsBrowsable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetHomeRecentlyAddedAsync(CancellationToken cancellationToken)
    {
        var albumsResult = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicAlbum],
            OrderBy = [MediaOrderingOption.CreatedDesc],
            PageNumber = 1,
            PageSize = 30
        }, cancellationToken);

        return (albumsResult?.Items ?? [])
            .Select(a => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{a.Id}",
                Title = a.Title ?? "Unknown Album",
                ArtworkUrl = GetPictureUrl(a.Pictures),
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetPlaylistTracksAsync(string parentId, CancellationToken cancellationToken)
    {
        var playlistId = Guid.Parse(parentId[PrefixPlaylist.Length..]);
        var playlist = await _playlistService.GetPlaylistAsync(playlistId, cancellationToken);
        var playlistCoverUrl = GetPictureUrl(playlist?.CoverPicture);
        var result = await _playlistService.GetPlaylistItemsAsync(playlistId, pageNumber: 1, pageSize: 500, cancellationToken: cancellationToken);

        var items = new List<MediaBrowseItem>
        {
            new() { Id = parentId, Title = "Play All", IsPlayable = true },
            new() { Id = $"{parentId}{ShuffleSuffix}", Title = "Shuffle", IsPlayable = true }
        };

        items.AddRange((result?.Items ?? [])
            .Select(item => new MediaBrowseItem
            {
                Id = $"{PrefixPlaylist}{playlistId}:{item.MediaId}",
                Title = item.MediaTitle ?? "Unknown Track",
                Subtitle = item.ArtistName,
                ArtworkUrl = GetPictureUrl(item.Pictures) ?? playlistCoverUrl,
                IsPlayable = true
            }));

        return items;
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetAllTracksAsQueueAsync(CancellationToken cancellationToken)
    {
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 1000
        }, cancellationToken);

        return (result?.Items ?? [])
            .OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new AudioQueueItem
            {
                IndexedFileId = t.IndexedFileId!.Value,
                MediaId = t.Id,
                Title = t.Title ?? "Unknown Track",
                Artist = t.ArtistName,
                ArtistId = t.ArtistId,
                AlbumTitle = t.AlbumTitle,
                Genre = t.Genre,
                CoverUrl = GetPictureUrl(t.Pictures),
                Duration = t.Duration
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetAlbumQueueAsync(string parentId, CancellationToken cancellationToken)
    {
        var albumId = Guid.Parse(parentId[PrefixAlbum.Length..]);
        var media = await _mediaService.GetMediaAsync(albumId, cancellationToken);

        if (media is not MusicAlbumDto album)
            return [];

        var coverUrl = GetPictureUrl(album.Pictures);

        return (album.Tracks ?? [])
            .OrderBy(t => t.TrackNumber)
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new AudioQueueItem
            {
                IndexedFileId = t.IndexedFileId!.Value,
                MediaId = t.Id,
                Title = t.Title ?? "Unknown Track",
                Artist = album.ArtistName,
                ArtistId = album.ArtistId,
                AlbumTitle = album.Title,
                Genre = album.Genres?.FirstOrDefault(),
                CoverUrl = coverUrl,
                Duration = t.Duration
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetSingleTrackQueueAsync(string parentId, CancellationToken cancellationToken)
    {
        var mediaId = Guid.Parse(parentId[PrefixTrack.Length..]);
        var media = await _mediaService.GetMediaAsync(mediaId, cancellationToken);

        if (media is not MusicTrackDto track)
            return [];

        var indexedFile = track.IndexedFiles?.FirstOrDefault();
        if (indexedFile is null)
            return [];

        return
        [
            new AudioQueueItem
            {
                IndexedFileId = indexedFile.Id,
                MediaId = track.Id,
                Title = track.Title ?? "Unknown Track",
                Artist = track.ArtistName,
                ArtistId = track.ArtistId,
                AlbumTitle = null,
                CoverUrl = GetPictureUrl(track.Pictures)
            }
        ];
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetPlaylistQueueAsync(string parentId, CancellationToken cancellationToken)
    {
        var playlistId = Guid.Parse(parentId[PrefixPlaylist.Length..]);
        var result = await _playlistService.GetPlaylistItemsAsync(playlistId, pageNumber: 1, pageSize: 500, cancellationToken: cancellationToken);

        return (result?.Items ?? [])
            .Where(item => item.IndexedFileId.HasValue)
            .Select(item => new AudioQueueItem
            {
                IndexedFileId = item.IndexedFileId!.Value,
                MediaId = item.MediaId,
                Title = item.MediaTitle ?? "Unknown Track",
                Artist = item.ArtistName,
                ArtistId = item.ArtistId,
                AlbumTitle = item.AlbumTitle,
                Genre = item.Genre,
                Duration = item.Duration,
                CoverUrl = GetPictureUrl(item.Pictures)
            })
            .ToArray();
    }

    private string? GetPictureUrl(IReadOnlyList<MetadataPictureDto>? pictures)
    {
        var picture = pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        return _apiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private string? GetPictureUrl(MetadataPictureDto? picture)
    {
        return _apiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    // --- Downloads (offline) ---

    private async Task<IReadOnlyList<MediaBrowseItem>> GetDownloadGroupsWithShuffleAsync(CancellationToken cancellationToken)
    {
        var items = await _offlineStore.GetByMediaTypeAsync(MediaType.MusicTrack, cancellationToken);

        var groups = items
            .GroupBy(i => i.AlbumTitle ?? i.Title)
            .Select(g => new MediaBrowseItem
            {
                Id = $"{PrefixDownloadGroup}{g.Key}",
                Title = g.Key,
                Subtitle = g.First().Artist,
                ArtworkUrl = g.First().CoverLocalPath is not null
                    ? $"file://{g.First().CoverLocalPath}"
                    : null,
                IsBrowsable = true,
                IsPlayable = true
            })
            .ToList();

        if (items.Count > 0)
        {
            groups.Insert(0, new MediaBrowseItem
            {
                Id = $"{RootDownloads}{ShuffleSuffix}",
                Title = "Shuffle All",
                Subtitle = $"{items.Count} tracks",
                IsPlayable = true
            });
        }

        return groups;
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetDownloadGroupTracksAsync(string parentId, CancellationToken cancellationToken)
    {
        var groupKey = parentId[PrefixDownloadGroup.Length..];
        var items = await _offlineStore.GetByMediaTypeAsync(MediaType.MusicTrack, cancellationToken);

        return items
            .Where(i => (i.AlbumTitle ?? i.Title) == groupKey)
            .Select(i => new MediaBrowseItem
            {
                Id = $"{PrefixDownloadGroup}{groupKey}:{i.MediaId}",
                Title = i.Title,
                Subtitle = i.Artist,
                ArtworkUrl = i.CoverLocalPath is not null ? $"file://{i.CoverLocalPath}" : null,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetDownloadQueueAsync(CancellationToken cancellationToken)
    {
        var items = await _offlineStore.GetByMediaTypeAsync(MediaType.MusicTrack, cancellationToken);

        return items
            .Select(i => new AudioQueueItem
            {
                IndexedFileId = i.IndexedFileId,
                MediaId = i.MediaId,
                Title = i.Title,
                Artist = i.Artist,
                AlbumTitle = i.AlbumTitle,
                CoverUrl = i.CoverLocalPath is not null ? $"file://{i.CoverLocalPath}" : null,
                LocalPath = i.MediaLocalPath
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetDownloadGroupQueueAsync(string parentId, CancellationToken cancellationToken)
    {
        var suffix = parentId[PrefixDownloadGroup.Length..];

        // Check if it's a track within a group: "download-group:GroupName:mediaGuid"
        // The last segment after ':' might be a Guid (track ID)
        var lastColon = suffix.LastIndexOf(':');
        string groupKey;
        Guid? targetTrackId = null;

        if (lastColon > 0 && Guid.TryParse(suffix[(lastColon + 1)..], out var parsedTrackId))
        {
            groupKey = suffix[..lastColon];
            targetTrackId = parsedTrackId;
        }
        else
        {
            groupKey = suffix;
        }

        var items = await _offlineStore.GetByMediaTypeAsync(MediaType.MusicTrack, cancellationToken);

        var queue = items
            .Where(i => (i.AlbumTitle ?? i.Title) == groupKey)
            .Select(i => new AudioQueueItem
            {
                IndexedFileId = i.IndexedFileId,
                MediaId = i.MediaId,
                Title = i.Title,
                Artist = i.Artist,
                AlbumTitle = i.AlbumTitle,
                CoverUrl = i.CoverLocalPath is not null ? $"file://{i.CoverLocalPath}" : null,
                LocalPath = i.MediaLocalPath
            })
            .ToArray();

        if (targetTrackId.HasValue)
        {
            var startIdx = Array.FindIndex(queue, q => q.MediaId == targetTrackId.Value);
            if (startIdx > 0)
                return queue.Skip(startIdx).Concat(queue.Take(startIdx)).ToArray();
        }

        return queue;
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetArtistQueueAsync(string parentId, CancellationToken cancellationToken)
    {
        var artistId = Guid.Parse(parentId[PrefixArtist.Length..]);
        var media = await _mediaService.GetMediaAsync(artistId, cancellationToken);

        if (media is not MusicArtistDto artist)
            return [];

        var queue = new List<AudioQueueItem>();

        foreach (var album in artist.Albums ?? [])
        {
            var albumMedia = await _mediaService.GetMediaAsync(album.Id, cancellationToken);
            if (albumMedia is not MusicAlbumDto fullAlbum) continue;

            var coverUrl = GetPictureUrl(fullAlbum.Pictures);

            queue.AddRange((fullAlbum.Tracks ?? [])
                .OrderBy(t => t.TrackNumber)
                .Where(t => t.IndexedFileId.HasValue)
                .Select(t => new AudioQueueItem
                {
                    IndexedFileId = t.IndexedFileId!.Value,
                    MediaId = t.Id,
                    Title = t.Title ?? "Unknown Track",
                    Artist = artist.Title,
                    ArtistId = artist.Id,
                    AlbumTitle = fullAlbum.Title,
                    CoverUrl = coverUrl,
                    Duration = t.Duration
                }));
        }

        return queue;
    }

    private static IReadOnlyList<AudioQueueItem> Shuffle(IReadOnlyList<AudioQueueItem> queue)
    {
        var list = queue.ToList();
        var rng = Random.Shared;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    public async Task<IReadOnlyList<MediaBrowseItem>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<MediaBrowseItem>();

        // Artists first (most likely what users search by name)
        var artistsResult = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicArtist],
            SearchText = query,
            PageNumber = 1,
            PageSize = 10
        }, cancellationToken);

        foreach (var item in artistsResult?.Items ?? [])
        {
            results.Add(new MediaBrowseItem
            {
                Id = $"{PrefixArtist}{item.Id}",
                Title = item.Title ?? "Unknown",
                ArtworkUrl = GetPictureUrl(item.Pictures),
                IsBrowsable = true
            });
        }

        // Albums
        var albumsResult = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicAlbum],
            SearchText = query,
            PageNumber = 1,
            PageSize = 10
        }, cancellationToken);

        foreach (var item in albumsResult?.Items ?? [])
        {
            results.Add(new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{item.Id}",
                Title = item.Title ?? "Unknown",
                ArtworkUrl = GetPictureUrl(item.Pictures),
                IsBrowsable = true,
                IsPlayable = true
            });
        }

        // Tracks
        var tracksResult = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            SearchText = query,
            PageNumber = 1,
            PageSize = 20
        }, cancellationToken);

        foreach (var item in tracksResult?.Items?.OfType<LiteMusicTrackDto>() ?? [])
        {
            results.Add(new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{item.AlbumId}:{item.Id}",
                Title = item.Title ?? "Unknown",
                Subtitle = item.ArtistName,
                ArtworkUrl = GetPictureUrl(item.Pictures),
                IsPlayable = true
            });
        }

        return results;
    }
}
