using System.Linq.Expressions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Helpers;

internal static class MediaLibraryLinkageHelper
{
    internal static Expression<Func<BaseMedia, bool>> LinkedToLibrary(Guid libraryId) =>
        m => m is MusicAlbum
            ? m.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId)
                || ((MusicAlbum)m).Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == libraryId)
                    || t.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId))
            : m is MusicArtist
                ? ((MusicArtist)m).Albums.Any(a => a.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId)
                    || a.Tracks.Any(t => t.IndexedFiles.Any(f => f.LibraryId == libraryId)
                        || t.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId)))
            : m is Serie
                ? m.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId)
                    || ((Serie)m).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => f.LibraryId == libraryId)
                        || e.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId)))
            : m is SerieSeason
                ? ((SerieSeason)m).Episodes.Any(e => e.IndexedFiles.Any(f => f.LibraryId == libraryId)
                    || e.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId))
            : m.IndexedFiles.Any(f => f.LibraryId == libraryId)
                || m.RemoteIndexedFiles.Any(r => r.LibraryId == libraryId);

    internal static IQueryable<BaseMedia> WhereLinkedToLibrary(this IQueryable<BaseMedia> query, Guid libraryId) =>
        query.Where(LinkedToLibrary(libraryId));

    internal static async Task<Library?> FindLibraryAsync(
        IApplicationDbContext context,
        BaseMedia media,
        CancellationToken cancellationToken = default)
    {
        var libraryId = await FindLibraryIdAsync(context, media, cancellationToken);
        if (libraryId is null)
            return null;

        return await context.Libraries.FindAsync([libraryId.Value], cancellationToken);
    }

    internal static async Task<Guid?> FindLibraryIdAsync(
        IApplicationDbContext context,
        BaseMedia media,
        CancellationToken cancellationToken = default)
    {
        var libraryId = media switch
        {
            MusicArtist => await context.Medias.OfType<MusicTrack>()
                .Where(t => t.Album!.ArtistId == media.Id)
                .SelectMany(t => context.IndexedFiles.Where(f => f.MediaId == t.Id).Select(f => (Guid?)f.LibraryId))
                .FirstOrDefaultAsync(cancellationToken),

            MusicAlbum album => await context.Medias.OfType<MusicTrack>()
                .Where(t => t.AlbumId == album.Id)
                .SelectMany(t => context.IndexedFiles.Where(f => f.MediaId == t.Id).Select(f => (Guid?)f.LibraryId))
                .FirstOrDefaultAsync(cancellationToken)
                ?? await context.IndexedFiles
                    .Where(f => f.MediaId == album.Id)
                    .Select(f => (Guid?)f.LibraryId)
                    .FirstOrDefaultAsync(cancellationToken),

            Serie serie => await context.Medias.OfType<SerieEpisode>()
                .Where(e => e.SerieId == serie.Id)
                .SelectMany(e => context.IndexedFiles.Where(f => f.MediaId == e.Id).Select(f => (Guid?)f.LibraryId))
                .FirstOrDefaultAsync(cancellationToken)
                ?? await context.IndexedFiles
                    .Where(f => f.MediaId == serie.Id)
                    .Select(f => (Guid?)f.LibraryId)
                    .FirstOrDefaultAsync(cancellationToken),

            SerieSeason season => await context.Medias.OfType<SerieEpisode>()
                .Where(e => e.SeasonId == season.Id)
                .SelectMany(e => context.IndexedFiles.Where(f => f.MediaId == e.Id).Select(f => (Guid?)f.LibraryId))
                .FirstOrDefaultAsync(cancellationToken),

            _ => await context.IndexedFiles
                .Where(f => f.MediaId == media.Id)
                .Select(f => (Guid?)f.LibraryId)
                .FirstOrDefaultAsync(cancellationToken)
        };

        libraryId ??= await context.RemoteIndexedFiles
            .Where(r => r.MediaId == media.Id)
            .Select(r => (Guid?)r.LibraryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (media is MusicAlbum albumForRemote)
        {
            libraryId ??= await context.RemoteIndexedFiles
                .Where(r => context.Medias.OfType<MusicTrack>().Any(t => t.AlbumId == albumForRemote.Id && t.Id == r.MediaId))
                .Select(r => (Guid?)r.LibraryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (media is MusicArtist)
        {
            libraryId ??= await context.RemoteIndexedFiles
                .Where(r => context.Medias.OfType<MusicAlbum>().Any(a => a.ArtistId == media.Id && r.MediaId == a.Id))
                .Select(r => (Guid?)r.LibraryId)
                .FirstOrDefaultAsync(cancellationToken);

            libraryId ??= await context.RemoteIndexedFiles
                .Where(r => context.Medias.OfType<MusicTrack>()
                    .Any(t => t.Album!.ArtistId == media.Id && t.Id == r.MediaId))
                .Select(r => (Guid?)r.LibraryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (media is Serie serieForRemote)
        {
            libraryId ??= await context.RemoteIndexedFiles
                .Where(r => context.Medias.OfType<SerieEpisode>().Any(e => e.SerieId == serieForRemote.Id && e.Id == r.MediaId))
                .Select(r => (Guid?)r.LibraryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (media is SerieSeason seasonForRemote)
        {
            libraryId ??= await context.RemoteIndexedFiles
                .Where(r => context.Medias.OfType<SerieEpisode>().Any(e => e.SeasonId == seasonForRemote.Id && e.Id == r.MediaId))
                .Select(r => (Guid?)r.LibraryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return libraryId;
    }

    internal static IQueryable<IndexedFile> GetIndexedFilesQuery(IApplicationDbContext context, BaseMedia media) =>
        media switch
        {
            MusicAlbum album => context.IndexedFiles
                .Where(f => f.MediaId == album.Id
                    || (f.MediaId != null
                        && context.Medias.OfType<MusicTrack>().Any(t => t.Id == f.MediaId && t.AlbumId == album.Id))),

            MusicArtist artist => context.IndexedFiles
                .Where(f => f.MediaId != null
                    && context.Medias.OfType<MusicTrack>().Any(t => t.Id == f.MediaId && t.Album!.ArtistId == artist.Id)),

            Serie serie => context.IndexedFiles
                .Where(f => f.MediaId == serie.Id
                    || (f.MediaId != null
                        && context.Medias.OfType<SerieEpisode>().Any(e => e.Id == f.MediaId && e.SerieId == serie.Id))),

            SerieSeason season => context.IndexedFiles
                .Where(f => f.MediaId != null
                    && context.Medias.OfType<SerieEpisode>().Any(e => e.Id == f.MediaId && e.SeasonId == season.Id)),

            _ => context.IndexedFiles.Where(f => f.MediaId == media.Id)
        };
}
