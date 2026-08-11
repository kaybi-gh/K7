using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Deletes music tracks/albums/artists that no longer have files (or children) and carry no user data.
/// </summary>
public static class MusicOrphanCleanupHelper
{
    public static async Task<bool> TryDeleteTrackIfOrphanAsync(
        IApplicationDbContext context,
        Guid trackId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var track = await context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.IndexedFiles)
            .Include(t => t.RemoteIndexedFiles)
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

        if (track is null)
            return false;

        if (track.IndexedFiles.Count > 0 || track.RemoteIndexedFiles.Count > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, trackId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan music track {TrackId} ({Title}) because user data exists",
                trackId,
                track.Title);
            return false;
        }

        var albumId = track.AlbumId;
        var trackArtistId = track.ArtistId;
        context.Medias.Remove(track);

        if (await CountRemainingTracksAsync(context, albumId, trackId, cancellationToken) == 0)
        {
            await TryDeleteAlbumIfOrphanAsync(
                context,
                albumId,
                logger,
                excludingTrackId: trackId,
                cancellationToken);
        }

        if (trackArtistId is Guid artistId)
        {
            await TryDeleteArtistIfOrphanAsync(
                context,
                artistId,
                logger,
                excludingTrackId: trackId,
                cancellationToken: cancellationToken);
        }

        logger.LogInformation(
            "Deleted orphan music track {TrackId} with no files and no user data",
            trackId);
        return true;
    }

    public static async Task<bool> TryDeleteAlbumIfOrphanAsync(
        IApplicationDbContext context,
        Guid albumId,
        ILogger logger,
        Guid? excludingTrackId = null,
        CancellationToken cancellationToken = default)
    {
        var album = await context.Medias
            .OfType<MusicAlbum>()
            .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

        if (album is null)
            return false;

        if (await CountRemainingTracksAsync(context, albumId, excludingTrackId, cancellationToken) > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, albumId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan music album {AlbumId} ({Title}) because user data exists",
                albumId,
                album.Title);
            return false;
        }

        var artistId = album.ArtistId;
        context.Medias.Remove(album);
        logger.LogInformation(
            "Deleted orphan music album {AlbumId} with no tracks and no user data",
            albumId);

        if (artistId is Guid id)
        {
            await TryDeleteArtistIfOrphanAsync(
                context,
                id,
                logger,
                excludingAlbumId: albumId,
                cancellationToken: cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Removes <paramref name="artistId"/> when it has no albums, tracks, or credits and no user data.
    /// </summary>
    public static async Task<bool> TryDeleteArtistIfOrphanAsync(
        IApplicationDbContext context,
        Guid artistId,
        ILogger logger,
        Guid? excludingAlbumId = null,
        Guid? excludingTrackId = null,
        CancellationToken cancellationToken = default)
    {
        var artist = await context.Medias
            .OfType<MusicArtist>()
            .FirstOrDefaultAsync(a => a.Id == artistId, cancellationToken);

        if (artist is null)
            return false;

        if (await CountRemainingAlbumsAsync(context, artistId, excludingAlbumId, cancellationToken) > 0)
            return false;

        if (await CountRemainingTracksForArtistAsync(context, artistId, excludingTrackId, cancellationToken) > 0)
            return false;

        if (await CountRemainingCreditsAsync(context, artistId, cancellationToken) > 0)
            return false;

        if (await MediaHasUserDataHelper.HasUserDataAsync(context, artistId, cancellationToken))
        {
            logger.LogInformation(
                "Keeping orphan music artist {ArtistId} ({Title}) because user data exists",
                artistId,
                artist.Title);
            return false;
        }

        context.Medias.Remove(artist);
        logger.LogInformation(
            "Deleted orphan music artist {ArtistId} with no albums, tracks, or credits and no user data",
            artistId);
        return true;
    }

    private static async Task<int> CountRemainingTracksAsync(
        IApplicationDbContext context,
        Guid albumId,
        Guid? excludingTrackId,
        CancellationToken cancellationToken)
    {
        var query = context.Medias.OfType<MusicTrack>().Where(t => t.AlbumId == albumId);
        if (excludingTrackId is Guid excludeId)
            query = query.Where(t => t.Id != excludeId);

        var count = await query.CountAsync(cancellationToken);

        foreach (var tracked in context.Medias.Local.OfType<MusicTrack>())
        {
            if (tracked.AlbumId != albumId)
                continue;
            if (excludingTrackId is Guid exclude && tracked.Id == exclude)
                continue;

            var state = context.Entry(tracked).State;
            if (state == EntityState.Added)
                count++;
            else if (state == EntityState.Deleted)
                count--;
        }

        return count;
    }

    private static async Task<int> CountRemainingAlbumsAsync(
        IApplicationDbContext context,
        Guid artistId,
        Guid? excludingAlbumId,
        CancellationToken cancellationToken)
    {
        var query = context.Medias.OfType<MusicAlbum>().Where(a => a.ArtistId == artistId);
        if (excludingAlbumId is Guid excludeId)
            query = query.Where(a => a.Id != excludeId);

        var count = await query.CountAsync(cancellationToken);

        foreach (var tracked in context.Medias.Local.OfType<MusicAlbum>())
        {
            if (excludingAlbumId is Guid exclude && tracked.Id == exclude)
                continue;
            if (tracked.ArtistId != artistId)
                continue;

            var state = context.Entry(tracked).State;
            if (state == EntityState.Added)
                count++;
            else if (state == EntityState.Deleted)
                count--;
        }

        return count;
    }

    private static async Task<int> CountRemainingTracksForArtistAsync(
        IApplicationDbContext context,
        Guid artistId,
        Guid? excludingTrackId,
        CancellationToken cancellationToken)
    {
        var query = context.Medias.OfType<MusicTrack>().Where(t => t.ArtistId == artistId);
        if (excludingTrackId is Guid excludeId)
            query = query.Where(t => t.Id != excludeId);

        var count = await query.CountAsync(cancellationToken);

        foreach (var tracked in context.Medias.Local.OfType<MusicTrack>())
        {
            if (excludingTrackId is Guid exclude && tracked.Id == exclude)
                continue;
            if (tracked.ArtistId != artistId)
                continue;

            var state = context.Entry(tracked).State;
            if (state == EntityState.Added)
                count++;
            else if (state == EntityState.Deleted)
                count--;
        }

        return count;
    }

    private static async Task<int> CountRemainingCreditsAsync(
        IApplicationDbContext context,
        Guid artistId,
        CancellationToken cancellationToken)
    {
        var count = await context.MusicArtistCredits
            .CountAsync(c => c.MusicArtistId == artistId, cancellationToken);

        foreach (var tracked in context.MusicArtistCredits.Local)
        {
            if (tracked.MusicArtistId != artistId)
                continue;

            var state = context.Entry(tracked).State;
            if (state == EntityState.Added)
                count++;
            else if (state == EntityState.Deleted)
                count--;
        }

        return count;
    }
}
