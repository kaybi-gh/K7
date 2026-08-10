using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Deletes music tracks/albums that no longer have files and carry no user data.
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

        context.Medias.Remove(album);
        logger.LogInformation(
            "Deleted orphan music album {AlbumId} with no tracks and no user data",
            albumId);
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
}
