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
    private const string RootAlbums = "root:albums";
    private const string RootArtists = "root:artists";
    private const string RootPlaylists = "root:playlists";
    private const string RootTracks = "root:tracks";

    private const string PrefixAlbum = "album:";
    private const string PrefixArtist = "artist:";
    private const string PrefixPlaylist = "playlist:";

    private readonly IMediaService _mediaService;
    private readonly IPlaylistService _playlistService;
    private readonly IK7ServerService _apiClient;

    public MediaBrowseService(
        IMediaService mediaService,
        IPlaylistService playlistService,
        IK7ServerService apiClient)
    {
        _mediaService = mediaService;
        _playlistService = playlistService;
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<MediaBrowseItem>> GetRootItemsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MediaBrowseItem> items =
        [
            new MediaBrowseItem { Id = RootAlbums, Title = "Albums", IsBrowsable = true },
            new MediaBrowseItem { Id = RootArtists, Title = "Artists", IsBrowsable = true },
            new MediaBrowseItem { Id = RootPlaylists, Title = "Playlists", IsBrowsable = true },
            new MediaBrowseItem { Id = RootTracks, Title = "Tracks", IsPlayable = true, IsBrowsable = true }
        ];

        return Task.FromResult(items);
    }

    public async Task<IReadOnlyList<MediaBrowseItem>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return parentId switch
        {
            RootAlbums => await GetAlbumsAsync(cancellationToken),
            RootArtists => await GetArtistsAsync(cancellationToken),
            RootPlaylists => await GetPlaylistsAsync(cancellationToken),
            RootTracks => await GetTracksAsBrowseItemsAsync(cancellationToken),
            _ when parentId.StartsWith(PrefixArtist) => await GetArtistAlbumsAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixAlbum) => await GetAlbumTracksAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixPlaylist) => await GetPlaylistTracksAsync(parentId, cancellationToken),
            _ => []
        };
    }

    public async Task<IReadOnlyList<AudioQueueItem>> GetPlayableItemsAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return parentId switch
        {
            RootTracks => await GetAllTracksAsQueueAsync(cancellationToken),
            _ when parentId.StartsWith(PrefixAlbum) => await GetAlbumQueueAsync(parentId, cancellationToken),
            _ when parentId.StartsWith(PrefixPlaylist) => await GetPlaylistQueueAsync(parentId, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetAlbumsAsync(CancellationToken cancellationToken)
    {
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicAlbum],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

        return (result?.Items ?? [])
            .OfType<LiteMusicAlbumDto>()
            .Select(a => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{a.Id}",
                Title = a.Title ?? "Unknown Album",
                ArtworkUrl = GetPictureUrl(a.Pictures),
                IsBrowsable = true,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetArtistsAsync(CancellationToken cancellationToken)
    {
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicArtist],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

        return (result?.Items ?? [])
            .OfType<LiteMusicArtistDto>()
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
            mediaType: MediaType.MusicTrack,
            cancellationToken: cancellationToken);

        return (result?.Items ?? [])
            .Select(p => new MediaBrowseItem
            {
                Id = $"{PrefixPlaylist}{p.Id}",
                Title = p.Title,
                Subtitle = $"{p.ItemCount} tracks",
                ArtworkUrl = GetPictureUrl(p.CoverPicture),
                IsBrowsable = true,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetTracksAsBrowseItemsAsync(CancellationToken cancellationToken)
    {
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

        return (result?.Items ?? [])
            .OfType<LiteMusicTrackDto>()
            .Select(t => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{t.AlbumId}:{t.Id}",
                Title = t.Title ?? "Unknown Track",
                Subtitle = t.ArtistName,
                ArtworkUrl = GetPictureUrl(t.Pictures),
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetArtistAlbumsAsync(string parentId, CancellationToken cancellationToken)
    {
        var artistId = Guid.Parse(parentId[PrefixArtist.Length..]);

        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicAlbum],
            ArtistIds = [artistId],
            OrderBy = [MediaOrderingOption.ReleaseDateDesc],
            PageNumber = 1,
            PageSize = 100
        }, cancellationToken);

        return (result?.Items ?? [])
            .OfType<LiteMusicAlbumDto>()
            .Select(a => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{a.Id}",
                Title = a.Title ?? "Unknown Album",
                Subtitle = a.ReleaseDate?.Year.ToString(),
                ArtworkUrl = GetPictureUrl(a.Pictures),
                IsBrowsable = true,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetAlbumTracksAsync(string parentId, CancellationToken cancellationToken)
    {
        var albumId = Guid.Parse(parentId[PrefixAlbum.Length..]);
        var media = await _mediaService.GetMediaAsync(albumId, cancellationToken);

        if (media is not MusicAlbumDto album)
            return [];

        var coverUrl = GetPictureUrl(album.Pictures);

        return (album.Tracks ?? [])
            .OrderBy(t => t.TrackNumber)
            .Select(t => new MediaBrowseItem
            {
                Id = $"{PrefixAlbum}{albumId}:{t.Id}",
                Title = t.Title ?? "Unknown Track",
                Subtitle = album.ArtistName,
                ArtworkUrl = coverUrl,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<MediaBrowseItem>> GetPlaylistTracksAsync(string parentId, CancellationToken cancellationToken)
    {
        var playlistId = Guid.Parse(parentId[PrefixPlaylist.Length..]);
        var result = await _playlistService.GetPlaylistItemsAsync(playlistId, pageNumber: 1, pageSize: 500, cancellationToken: cancellationToken);

        return (result?.Items ?? [])
            .Select(item => new MediaBrowseItem
            {
                Id = $"{PrefixPlaylist}{playlistId}:{item.MediaId}",
                Title = item.MediaTitle ?? "Unknown Track",
                Subtitle = item.ArtistName,
                IsPlayable = true
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AudioQueueItem>> GetAllTracksAsQueueAsync(CancellationToken cancellationToken)
    {
        var result = await _mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 200
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
                Duration = t.Duration,
                Bpm = t.Bpm,
                MusicalKey = t.MusicalKey,
                Energy = t.Energy
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
                Duration = t.Duration,
                Bpm = t.Bpm,
                MusicalKey = t.MusicalKey,
                Energy = t.Energy
            })
            .ToArray();
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
                Duration = item.Duration
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
}
