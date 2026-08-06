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
    private async Task<OpenSubsonicActionResult> GetMusicFoldersAsync(CancellationToken cancellationToken)
    {
        var folders = await context.Libraries
            .AsNoTracking()
            .Where(l => l.MediaType == LibraryMediaType.Music && l.PeerServerId == null)
            .OrderBy(l => l.Title)
            .Select(l => new OpenSubsonicMusicFolder
            {
                Id = l.Id.ToString("D"),
                Name = l.Title
            })
            .ToListAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["musicFolders"] = new Dictionary<string, object?> { ["musicFolder"] = folders }
        });
    }

    private async Task<OpenSubsonicActionResult> GetAlbumList2Async(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken,
        string key = "albumList2")
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var type = GetParam(parameters, "type") ?? "alphabeticalByName";
        var size = GetInt(parameters, "size", 10, 1, 500);
        var offset = GetInt(parameters, "offset", 0, 0, int.MaxValue);
        var fromYear = GetNullableInt(parameters, "fromYear");
        var toYear = GetNullableInt(parameters, "toYear");
        var genre = GetParam(parameters, "genre");
        var musicFolderId = GetGuid(parameters, "musicFolderId");

        var albums = await GetAccessibleAlbumsQueryAsync(userId.Value, musicFolderId, cancellationToken);

        if (fromYear is not null)
            albums = albums.Where(a => a.ReleaseDate != null && a.ReleaseDate.Value.Year >= fromYear.Value);
        if (toYear is not null)
            albums = albums.Where(a => a.ReleaseDate != null && a.ReleaseDate.Value.Year <= toYear.Value);
        if (!string.IsNullOrWhiteSpace(genre))
            albums = albums.Where(a => a.MetadataTags.Any(mt =>
                mt.MetadataTag.Kind == MetadataTagKind.Genre
                && mt.MetadataTag.DisplayName == genre));

        albums = type.ToLowerInvariant() switch
        {
            "newest" => albums.OrderByDescending(a => a.Created),
            "highest" => albums
                .OrderByDescending(a => a.Ratings.OfType<UserRating>().Where(r => r.UserId == userId).Select(r => (double?)r.Value).FirstOrDefault() ?? 0),
            "frequent" => albums
                .OrderByDescending(a => a.UserMediaStates.Where(s => s.UserId == userId).Select(s => (int?)s.PlayCount).FirstOrDefault() ?? 0),
            "recent" => albums
                .OrderByDescending(a => a.UserMediaStates.Where(s => s.UserId == userId).Select(s => s.LastInteractedAt).FirstOrDefault()),
            "alphabeticalbyartist" => albums.OrderBy(a => a.Artist != null ? a.Artist.SortTitle ?? a.Artist.Title : "")
                .ThenBy(a => a.SortTitle ?? a.Title),
            "bygenre" => albums.OrderBy(a => a.SortTitle ?? a.Title),
            "byyear" => albums.OrderBy(a => a.ReleaseDate).ThenBy(a => a.SortTitle ?? a.Title),
            "random" => albums.OrderBy(_ => EF.Functions.Random()),
            _ => albums.OrderBy(a => a.SortTitle ?? a.Title)
        };

        var pageAlbums = await albums
            .Skip(offset)
            .Take(size)
            .Include(a => a.Artist)
            .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Ratings)
            .Include(a => a.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var albumIds = pageAlbums.Select(a => a.Id).ToList();
        var trackCounts = albumIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await context.Medias.OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => albumIds.Contains(t.AlbumId))
                .GroupBy(t => t.AlbumId)
                .Select(g => new { AlbumId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AlbumId, x => x.Count, cancellationToken);

        var mapped = pageAlbums
            .Select(a => MapAlbum(a, userId.Value, includeSongs: false, songCount: trackCounts.GetValueOrDefault(a.Id)))
            .ToList();
        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [key] = new Dictionary<string, object?> { ["album"] = mapped }
        });
    }

    private async Task<OpenSubsonicActionResult> GetAlbumAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var album = await context.Medias.OfType<MusicAlbum>()
            .AsNoTracking()
            .Include(a => a.Artist)
            .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Tracks).ThenInclude(t => t.Artist)
            .Include(a => a.Tracks).ThenInclude(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(a => a.Tracks).ThenInclude(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Tracks).ThenInclude(t => t.Ratings)
            .Include(a => a.Tracks).ThenInclude(t => t.UserMediaStates)
            .Include(a => a.Ratings)
            .Include(a => a.UserMediaStates)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id.Value, cancellationToken);

        if (album is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Album not found.");

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["album"] = MapAlbum(album, userId.Value, includeSongs: true)
        });
    }

    private async Task<OpenSubsonicActionResult> GetSongAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var track = await LoadTrackAsync(id.Value, cancellationToken);
        if (track is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Song not found.");

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["song"] = MapSong(track, userId.Value)
        });
    }


    private async Task<OpenSubsonicActionResult> GetArtistsAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var musicFolderId = GetGuid(parameters, "musicFolderId");
        var artists = await (await GetAccessibleArtistsQueryAsync(userId.Value, musicFolderId, cancellationToken))
            .OrderBy(a => a.SortTitle ?? a.Title)
            .Include(a => a.Albums)
            .Include(a => a.Ratings)
            .ToListAsync(cancellationToken);

        var indexes = artists
            .GroupBy(a => GetIndexName(a.SortTitle ?? a.Title))
            .OrderBy(g => g.Key)
            .Select(g => new OpenSubsonicIndex
            {
                Name = g.Key,
                Artist = g.Select(a => MapArtist(a, userId.Value)).ToList()
            })
            .ToList();

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["artists"] = new Dictionary<string, object?>
            {
                ["index"] = indexes,
                ["ignoredArticles"] = "The El La Los Las Le Les"
            }
        });
    }

    private async Task<OpenSubsonicActionResult> GetArtistAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var artist = await context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .Include(a => a.Albums).ThenInclude(al => al.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Albums).ThenInclude(al => al.Tracks)
            .Include(a => a.Albums).ThenInclude(al => al.Ratings)
            .Include(a => a.Albums).ThenInclude(al => al.UserMediaStates)
            .Include(a => a.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id.Value, cancellationToken);

        if (artist is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Artist not found.");

        var mapped = MapArtist(artist, userId.Value);
        mapped = new OpenSubsonicArtist
        {
            Id = mapped.Id,
            Name = mapped.Name,
            AlbumCount = artist.Albums.Count,
            CoverArt = mapped.CoverArt,
            Starred = mapped.Starred,
            UserRating = mapped.UserRating,
            Album = artist.Albums
                .OrderBy(a => a.ReleaseDate)
                .ThenBy(a => a.SortTitle ?? a.Title)
                .Select(a => MapAlbum(a, userId.Value, includeSongs: false))
                .ToList()
        };

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?> { ["artist"] = mapped });
    }

    private async Task<OpenSubsonicActionResult> GetArtistInfoAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string responseKey,
        CancellationToken cancellationToken)
    {
        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var artistId = await ResolveArtistIdAsync(id.Value, cancellationToken);
        if (artistId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Artist not found.");

        await accessGuard.EnsureAccessAsync(artistId.Value, cancellationToken);

        var artist = await context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .Include(a => a.ExternalIds)
            .FirstOrDefaultAsync(a => a.Id == artistId.Value, cancellationToken);

        if (artist is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Artist not found.");

        List<OpenSubsonicArtist>? similar = null;
        try
        {
            var count = GetInt(parameters, "count", 20, 0, 100);
            if (count > 0)
            {
                var matches = await sender.Send(new GetSimilarMusicArtistsQuery
                {
                    ArtistId = artistId.Value,
                    Count = count
                }, cancellationToken);

                similar = matches.Select(m => new OpenSubsonicArtist
                {
                    Id = m.Id.ToString("D"),
                    Name = m.Title ?? string.Empty
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Similar artists unavailable for {ArtistId}", artistId.Value);
        }

        var mbId = artist.ExternalIds.FirstOrDefault(e =>
            e.ProviderName.Contains("musicbrainz", StringComparison.OrdinalIgnoreCase))?.Value;

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [responseKey] = new OpenSubsonicArtistInfo
            {
                Biography = artist.Biography,
                MusicBrainzId = mbId,
                SimilarArtist = similar
            }
        });
    }

    private async Task<OpenSubsonicActionResult> GetAlbumInfoAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string responseKey,
        CancellationToken cancellationToken)
    {
        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var albumId = await ResolveAlbumIdAsync(id.Value, cancellationToken);
        if (albumId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Album not found.");

        await accessGuard.EnsureAccessAsync(albumId.Value, cancellationToken);

        var album = await context.Medias.OfType<MusicAlbum>()
            .AsNoTracking()
            .Include(a => a.ExternalIds)
            .FirstOrDefaultAsync(a => a.Id == albumId.Value, cancellationToken);

        if (album is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Album not found.");

        var mbId = album.ExternalIds.FirstOrDefault(e =>
            e.ProviderName.Contains("musicbrainz", StringComparison.OrdinalIgnoreCase))?.Value;

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [responseKey] = new OpenSubsonicAlbumInfo
            {
                Notes = album.Overview,
                MusicBrainzId = mbId
            }
        });
    }

    private async Task<Guid?> ResolveArtistIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (media is null)
            return null;

        if (media.Type == MediaType.MusicArtist)
            return media.Id;

        if (media.Type == MediaType.MusicAlbum)
        {
            return await context.Medias.OfType<MusicAlbum>()
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => a.ArtistId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (media.Type == MediaType.MusicTrack)
        {
            return await context.Medias.OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => t.ArtistId ?? t.Album.ArtistId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private async Task<Guid?> ResolveAlbumIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (media is null)
            return null;

        if (media.Type == MediaType.MusicAlbum)
            return media.Id;

        if (media.Type == MediaType.MusicTrack)
        {
            return await context.Medias.OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => (Guid?)t.AlbumId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private async Task<OpenSubsonicActionResult> GetIndexesAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        // Same shape as getArtists for clients that still call getIndexes.
        var result = await GetArtistsAsync(parameters, cancellationToken);
        if (result.IsFailed || result.Data is null)
            return result;

        if (result.Data.TryGetValue("artists", out var artistsObj) && artistsObj is Dictionary<string, object?> artists)
        {
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["indexes"] = new Dictionary<string, object?>
                {
                    ["index"] = artists.GetValueOrDefault("index"),
                    ["ignoredArticles"] = artists.GetValueOrDefault("ignoredArticles"),
                    ["lastModified"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            });
        }

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["indexes"] = new Dictionary<string, object?> { ["index"] = Array.Empty<OpenSubsonicIndex>() }
        });
    }

    private async Task<OpenSubsonicActionResult> GetMusicDirectoryAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var library = await context.Libraries.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id.Value && l.MediaType == LibraryMediaType.Music, cancellationToken);
        if (library is not null)
        {
            var albums = await (await GetAccessibleAlbumsQueryAsync(userId.Value, library.Id, cancellationToken))
                .OrderBy(a => a.SortTitle ?? a.Title)
                .Include(a => a.Artist)
                .Include(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
                .Include(a => a.Tracks)
                .Include(a => a.Ratings)
                .Include(a => a.UserMediaStates)
                .AsSplitQuery()
                .Take(500)
                .ToListAsync(cancellationToken);

            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["directory"] = new OpenSubsonicDirectory
                {
                    Id = library.Id.ToString("D"),
                    Name = library.Title,
                    Child = albums.Select(a => (object)(MapAlbum(a, userId.Value, includeSongs: false) with { IsDir = true })).ToList()
                }
            });
        }

        await accessGuard.EnsureAccessAsync(id.Value, cancellationToken);

        var artist = await context.Medias.OfType<MusicArtist>()
            .AsNoTracking()
            .Include(a => a.Albums).ThenInclude(al => al.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Albums).ThenInclude(al => al.Tracks)
            .Include(a => a.Albums).ThenInclude(al => al.Ratings)
            .Include(a => a.Albums).ThenInclude(al => al.UserMediaStates)
            .Include(a => a.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id.Value, cancellationToken);

        if (artist is not null)
        {
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["directory"] = new OpenSubsonicDirectory
                {
                    Id = artist.Id.ToString("D"),
                    Name = artist.Title ?? string.Empty,
                    Child = artist.Albums
                        .OrderBy(a => a.ReleaseDate)
                        .Select(a => (object)(MapAlbum(a, userId.Value, includeSongs: false) with
                        {
                            IsDir = true,
                            Parent = artist.Id.ToString("D")
                        }))
                        .ToList()
                }
            });
        }

        var album = await context.Medias.OfType<MusicAlbum>()
            .AsNoTracking()
            .Include(a => a.Artist)
            .Include(a => a.Tracks).ThenInclude(t => t.Artist)
            .Include(a => a.Tracks).ThenInclude(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(a => a.Tracks).ThenInclude(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(a => a.Tracks).ThenInclude(t => t.Ratings)
            .Include(a => a.Tracks).ThenInclude(t => t.UserMediaStates)
            .Include(a => a.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id.Value, cancellationToken);

        if (album is not null)
        {
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["directory"] = new OpenSubsonicDirectory
                {
                    Id = album.Id.ToString("D"),
                    Parent = album.ArtistId?.ToString("D"),
                    Name = album.Title ?? string.Empty,
                    Child = album.Tracks
                        .OrderBy(t => t.DiscNumber)
                        .ThenBy(t => t.TrackNumber)
                        .Select(t => (object)MapSong(t, userId.Value))
                        .ToList()
                }
            });
        }

        return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, "Directory not found.");
    }


    private async Task<OpenSubsonicActionResult> GetRandomSongsAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var size = GetInt(parameters, "size", 10, 1, 500);
        var genre = GetParam(parameters, "genre");
        var fromYear = GetNullableInt(parameters, "fromYear");
        var toYear = GetNullableInt(parameters, "toYear");
        var musicFolderId = GetGuid(parameters, "musicFolderId");

        var query = await GetAccessibleTracksQueryAsync(userId.Value, musicFolderId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(t =>
                t.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && mt.MetadataTag.DisplayName == genre)
                || t.Album.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && mt.MetadataTag.DisplayName == genre));
        }

        if (fromYear is not null)
            query = query.Where(t => t.Album.ReleaseDate != null && t.Album.ReleaseDate.Value.Year >= fromYear.Value);
        if (toYear is not null)
            query = query.Where(t => t.Album.ReleaseDate != null && t.Album.ReleaseDate.Value.Year <= toYear.Value);

        var tracks = await query
            .OrderBy(_ => EF.Functions.Random())
            .Take(size)
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["randomSongs"] = new Dictionary<string, object?>
            {
                ["song"] = tracks.Select(t => MapSong(t, userId.Value)).ToList()
            }
        });
    }

    private async Task<OpenSubsonicActionResult> GetSongsByGenreAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var genre = GetParam(parameters, "genre");
        if (string.IsNullOrWhiteSpace(genre))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing genre.");

        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var count = GetInt(parameters, "count", 10, 1, 500);
        var offset = GetInt(parameters, "offset", 0, 0, int.MaxValue);
        var musicFolderId = GetGuid(parameters, "musicFolderId");

        var query = await GetAccessibleTracksQueryAsync(userId.Value, musicFolderId, cancellationToken);
        query = query.Where(t =>
            t.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && mt.MetadataTag.DisplayName == genre)
            || t.Album.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && mt.MetadataTag.DisplayName == genre));

        var tracks = await query
            .OrderBy(t => t.SortTitle ?? t.Title)
            .Skip(offset)
            .Take(count)
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["songsByGenre"] = new Dictionary<string, object?>
            {
                ["song"] = tracks.Select(t => MapSong(t, userId.Value)).ToList()
            }
        });
    }

    private async Task<OpenSubsonicActionResult> GetGenresAsync(CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var tracks = await GetAccessibleTracksQueryAsync(userId.Value, null, cancellationToken);
        var genres = await tracks
            .SelectMany(t => t.MetadataTags
                .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(mt => mt.MetadataTag.DisplayName)
                .Concat(t.Album.MetadataTags
                    .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                    .Select(mt => mt.MetadataTag.DisplayName)))
            .Where(g => g != null && g != "")
            .GroupBy(g => g!)
            .Select(g => new OpenSubsonicGenre
            {
                Value = g.Key,
                SongCount = g.Count(),
                AlbumCount = 0
            })
            .OrderBy(g => g.Value)
            .ToListAsync(cancellationToken);

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["genres"] = new Dictionary<string, object?> { ["genre"] = genres }
        });
    }

    private async Task<OpenSubsonicActionResult> GetSimilarSongsAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        string responseKey,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var id = GetGuid(parameters, "id");
        if (id is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing id.");

        var count = GetInt(parameters, "count", 50, 1, 500);
        var songs = new List<OpenSubsonicSong>();

        var seedTrackId = await ResolveSimilarSongsSeedTrackIdAsync(id.Value, cancellationToken);
        if (seedTrackId is null)
        {
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                [responseKey] = new Dictionary<string, object?> { ["song"] = songs }
            });
        }

        try
        {
            var matches = await sender.Send(new GetSimilarTracksQuery(seedTrackId.Value, count), cancellationToken);
            var mediaIds = matches.Select(m => m.ItemId).Where(m => m != Guid.Empty).Distinct().ToList();
            if (mediaIds.Count > 0)
            {
                var tracks = await LoadMappedTracksAsync(mediaIds, cancellationToken);
                var byId = tracks.ToDictionary(t => t.Id);
                songs = mediaIds.Where(byId.ContainsKey).Select(mid => MapSong(byId[mid], userId.Value)).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Similar songs unavailable for seed {TrackId}", seedTrackId.Value);
        }

        // Local fallback when Music Intelligence is off / empty (Tempus Instant Mix needs a non-empty list).
        if (songs.Count == 0)
        {
            var fallbackIds = await GetSimilarSongsFallbackIdsAsync(
                userId.Value, seedTrackId.Value, count, cancellationToken);
            if (fallbackIds.Count > 0)
            {
                var tracks = await LoadMappedTracksAsync(fallbackIds, cancellationToken);
                var byId = tracks.ToDictionary(t => t.Id);
                songs = fallbackIds.Where(byId.ContainsKey).Select(mid => MapSong(byId[mid], userId.Value)).ToList();
            }
        }

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            [responseKey] = new Dictionary<string, object?> { ["song"] = songs }
        });
    }

    private async Task<List<Guid>> GetSimilarSongsFallbackIdsAsync(
        Guid userId,
        Guid seedTrackId,
        int count,
        CancellationToken cancellationToken)
    {
        var seed = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => t.Id == seedTrackId)
            .Select(t => new { t.Id, t.ArtistId, AlbumArtistId = t.Album.ArtistId })
            .FirstOrDefaultAsync(cancellationToken);

        if (seed is null)
            return [];

        var artistId = seed.ArtistId ?? seed.AlbumArtistId;
        var query = await GetAccessibleTracksQueryAsync(userId, null, cancellationToken);
        query = query.Where(t => t.Id != seedTrackId);

        var ids = new List<Guid>();
        if (artistId is not null)
        {
            ids = await query
                .Where(t => t.ArtistId == artistId || t.Album.ArtistId == artistId)
                .OrderBy(_ => EF.Functions.Random())
                .Take(count)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        if (ids.Count >= Math.Min(count, 10))
            return ids;

        var exclude = ids.Append(seedTrackId).ToHashSet();
        var filler = await query
            .Where(t => !exclude.Contains(t.Id))
            .OrderBy(_ => EF.Functions.Random())
            .Take(count - ids.Count)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        ids.AddRange(filler);
        return ids;
    }

    private async Task<List<MusicTrack>> LoadMappedTracksAsync(
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken) =>
        await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => mediaIds.Contains(t.Id))
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    private async Task<Guid?> ResolveSimilarSongsSeedTrackIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (media is null)
            return null;

        if (media.Type == MediaType.MusicTrack)
            return media.Id;

        if (media.Type == MediaType.MusicAlbum)
        {
            return await context.Medias.OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => t.AlbumId == id)
                .OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (media.Type == MediaType.MusicArtist)
        {
            return await context.Medias.OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => t.ArtistId == id || t.Album.ArtistId == id)
                .OrderBy(t => t.Title)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private async Task<OpenSubsonicActionResult> GetTopSongsAsync(
        IReadOnlyDictionary<string, string[]> parameters,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserIdAsync(cancellationToken);
        if (userId is null)
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotAuthenticated, "Not authenticated.");

        var artistName = GetParam(parameters, "artist");
        if (string.IsNullOrWhiteSpace(artistName))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing artist.");

        var count = GetInt(parameters, "count", 50, 1, 500);
        var artistLike = EfLikeQueryExtensions.ToContainsPattern(artistName);
        var artist = await (await GetAccessibleArtistsQueryAsync(userId.Value, null, cancellationToken))
            .Where(a => a.Title != null && EfLikeQueryExtensions.ILike(a.Title, artistLike))
            .FirstOrDefaultAsync(cancellationToken);

        if (artist is null)
        {
            return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
            {
                ["topSongs"] = new Dictionary<string, object?> { ["song"] = Array.Empty<OpenSubsonicSong>() }
            });
        }

        var top = await sender.Send(new GetArtistTopTracksQuery
        {
            ArtistId = artist.Id,
            Count = count
        }, cancellationToken);

        var ids = top.Select(t => t.Id).ToList();
        var tracks = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var byId = tracks.ToDictionary(t => t.Id);
        var songs = ids.Where(byId.ContainsKey).Select(mid => MapSong(byId[mid], userId.Value)).ToList();

        return OpenSubsonicActionResult.Ok(new Dictionary<string, object?>
        {
            ["topSongs"] = new Dictionary<string, object?> { ["song"] = songs }
        });
    }

}
