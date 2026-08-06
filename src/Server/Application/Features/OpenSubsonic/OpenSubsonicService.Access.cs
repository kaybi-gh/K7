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
    private async Task<Guid?> RequireUserIdAsync(CancellationToken cancellationToken) =>
        await currentUser.GetIdAsync(cancellationToken);

    private async Task<IQueryable<MusicAlbum>> GetAccessibleAlbumsQueryAsync(
        Guid userId,
        Guid? musicFolderId,
        CancellationToken cancellationToken)
    {
        var baseQuery = await mediaAccessFilter.ApplyAllAsync(context.Medias.AsNoTracking(), userId, cancellationToken);
        var albums = baseQuery.OfType<MusicAlbum>();
        if (musicFolderId is not null)
        {
            albums = albums.Where(a => context.MediaLibraryAvailabilities.Any(m =>
                m.MediaId == a.Id && m.LibraryId == musicFolderId.Value));
        }

        return albums;
    }

    private async Task<IQueryable<MusicTrack>> GetAccessibleTracksQueryAsync(
        Guid userId,
        Guid? musicFolderId,
        CancellationToken cancellationToken)
    {
        var baseQuery = await mediaAccessFilter.ApplyAllAsync(context.Medias.AsNoTracking(), userId, cancellationToken);
        var tracks = baseQuery.OfType<MusicTrack>();
        if (musicFolderId is not null)
        {
            tracks = tracks.Where(t => context.MediaLibraryAvailabilities.Any(m =>
                m.MediaId == t.Id && m.LibraryId == musicFolderId.Value));
        }

        return tracks;
    }

    private async Task<IQueryable<MusicArtist>> GetAccessibleArtistsQueryAsync(
        Guid userId,
        Guid? musicFolderId,
        CancellationToken cancellationToken)
    {
        var baseQuery = await mediaAccessFilter.ApplyAllAsync(context.Medias.AsNoTracking(), userId, cancellationToken);
        var artists = baseQuery.OfType<MusicArtist>();
        if (musicFolderId is not null)
        {
            artists = artists.Where(a => a.Albums.Any(al => context.MediaLibraryAvailabilities.Any(m =>
                m.MediaId == al.Id && m.LibraryId == musicFolderId.Value)));
        }

        return artists;
    }

    private async Task<MusicTrack?> LoadTrackAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Artist)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Ratings)
            .Include(t => t.UserMediaStates)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    private async Task<string?> ResolveCoverPathAsync(Guid id, CancellationToken cancellationToken)
    {
        var direct = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => (p.MediaId == id || p.PlaylistId == id)
                        && p.Type == MetadataPictureType.Cover
                        && p.LocalPath != null)
            .OrderByDescending(p => p.Created)
            .Select(p => p.LocalPath)
            .FirstOrDefaultAsync(cancellationToken);

        if (direct is not null)
            return direct;

        var trackAlbumId = await context.Medias.OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (Guid?)t.AlbumId)
            .FirstOrDefaultAsync(cancellationToken);

        if (trackAlbumId is not null)
        {
            return await context.MetadataPictures
                .AsNoTracking()
                .Where(p => p.MediaId == trackAlbumId && p.Type == MetadataPictureType.Cover && p.LocalPath != null)
                .OrderByDescending(p => p.Created)
                .Select(p => p.LocalPath)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.MediaId == id && p.LocalPath != null)
            .OrderByDescending(p => p.Created)
            .Select(p => p.LocalPath)
            .FirstOrDefaultAsync(cancellationToken);
    }

}
