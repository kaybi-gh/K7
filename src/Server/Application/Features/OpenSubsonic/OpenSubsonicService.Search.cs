using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Application.Features.Medias.Queries.GetSimilarMusicArtists;
using K7.Server.Application.Features.MusicIntelligence.Queries.GetSimilarTracks;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;
using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private async Task<OpenSubsonicActionResult> SearchAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken,
        string resultKey)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var query = GetParam(parameters, "query") ?? string.Empty;
        var artistCount = GetInt(parameters, "artistCount", 20, 0, 500);
        var artistOffset = GetInt(parameters, "artistOffset", 0, 0, int.MaxValue);
        var albumCount = GetInt(parameters, "albumCount", 20, 0, 500);
        var albumOffset = GetInt(parameters, "albumOffset", 0, 0, int.MaxValue);
        var songCount = GetInt(parameters, "songCount", 20, 0, 500);
        var songOffset = GetInt(parameters, "songOffset", 0, 0, int.MaxValue);
        var musicFolderId = GetGuid(parameters, "musicFolderId");

        var pattern = query.Trim();
        var artists = new List<OpenSubsonicArtist>();
        var albums = new List<OpenSubsonicAlbum>();
        var songs = new List<OpenSubsonicSong>();

        if (artistCount > 0)
        {
            var artistQuery = await GetAccessibleArtistsQueryAsync(userId.Value, musicFolderId, cancellationToken);
            if (!string.IsNullOrEmpty(pattern))
            {
                var like = EfLikeQueryExtensions.ToContainsPattern(pattern);
                artistQuery = artistQuery.Where(a => a.Title != null && EfLikeQueryExtensions.ILike(a.Title, like));
            }

            var artistEntities = await artistQuery
                .OrderBy(a => a.SortTitle ?? a.Title)
                .Skip(artistOffset)
                .Take(artistCount)
                .Include(a => a.Ratings)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            artists = artistEntities.Select(a => MapArtist(a, userId.Value)).ToList();
        }

        if (albumCount > 0)
        {
            var albumQuery = await GetAccessibleAlbumsQueryAsync(userId.Value, musicFolderId, cancellationToken);
            if (!string.IsNullOrEmpty(pattern))
            {
                var like = EfLikeQueryExtensions.ToContainsPattern(pattern);
                albumQuery = albumQuery.Where(a => a.Title != null && EfLikeQueryExtensions.ILike(a.Title, like));
            }

            var albumEntities = await albumQuery
                .OrderBy(a => a.SortTitle ?? a.Title)
                .Skip(albumOffset)
                .Take(albumCount)
                .Include(a => a.Artist)
                .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
                .Include(a => a.Ratings)
                .Include(a => a.UserMediaStates)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var albumIds = albumEntities.Select(a => a.Id).ToList();
            var trackCounts = albumIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await context.Medias.OfType<MusicTrack>()
                    .AsNoTracking()
                    .Where(t => albumIds.Contains(t.AlbumId))
                    .GroupBy(t => t.AlbumId)
                    .Select(g => new { AlbumId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.AlbumId, x => x.Count, cancellationToken);

            albums = albumEntities
                .Select(a => MapAlbum(a, userId.Value, includeSongs: false, songCount: trackCounts.GetValueOrDefault(a.Id)))
                .ToList();
        }

        if (songCount > 0)
        {
            var songQuery = await GetAccessibleTracksQueryAsync(userId.Value, musicFolderId, cancellationToken);
            if (!string.IsNullOrEmpty(pattern))
            {
                var like = EfLikeQueryExtensions.ToContainsPattern(pattern);
                songQuery = songQuery.Where(t => t.Title != null && EfLikeQueryExtensions.ILike(t.Title, like));
            }

            var songEntities = await songQuery
                .OrderBy(t => t.SortTitle ?? t.Title)
                .Skip(songOffset)
                .Take(songCount)
                .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
                .Include(t => t.Artist)
                .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
                .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
                .Include(t => t.Ratings)
                .Include(t => t.UserMediaStates)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            songs = songEntities.Select(t => MapSong(t, userId.Value)).ToList();
        }

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [resultKey] = new Dictionary<string, object?>
            {
                ["artist"] = artists,
                ["album"] = albums,
                ["song"] = songs
            }
        });
    }

}
