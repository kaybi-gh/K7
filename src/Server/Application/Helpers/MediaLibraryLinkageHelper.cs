using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Helpers;

internal readonly record struct MediaLibraryPair(Guid LibraryId, Guid MediaId);

internal sealed class MediaLibraryPairProjection
{
    public Guid LibraryId { get; set; }
    public Guid MediaId { get; set; }
}

internal static class MediaLibraryLinkageHelper
{
    internal static IQueryable<BaseMedia> WhereLinkedToLibrary(
        this IQueryable<BaseMedia> query,
        IApplicationDbContext context,
        Guid libraryId) =>
        query.Where(m => context.MediaLibraryAvailabilities.Any(a => a.MediaId == m.Id && a.LibraryId == libraryId));

    internal static IQueryable<MediaLibraryPairProjection> SelectMediaLibraryPairs(IApplicationDbContext context)
    {
        var tracks = context.Medias.OfType<MusicTrack>();
        var albums = context.Medias.OfType<MusicAlbum>();
        var episodes = context.Medias.OfType<SerieEpisode>();

        var fromIndexed = context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Select(f => new { f.LibraryId, MediaId = f.MediaId!.Value });

        var fromRemote = context.RemoteIndexedFiles
            .Select(r => new { r.LibraryId, r.MediaId });

        var albumFromTracksIndexed = context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Join(tracks, f => f.MediaId!.Value, t => t.Id, (f, t) => new { f.LibraryId, MediaId = t.AlbumId });

        var albumFromTracksRemote = context.RemoteIndexedFiles
            .Join(tracks, r => r.MediaId, t => t.Id, (r, t) => new { r.LibraryId, MediaId = t.AlbumId });

        var artistFromAlbumsRemote = context.RemoteIndexedFiles
            .Join(albums.Where(a => a.ArtistId != null), r => r.MediaId, a => a.Id, (r, a) => new { r.LibraryId, MediaId = a.ArtistId!.Value });

        var artistFromTracksIndexed = context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Join(tracks, f => f.MediaId!.Value, t => t.Id, (f, t) => new { f.LibraryId, t.AlbumId })
            .Join(albums.Where(a => a.ArtistId != null), x => x.AlbumId, a => a.Id, (x, a) => new { x.LibraryId, MediaId = a.ArtistId!.Value });

        var artistFromTracksRemote = context.RemoteIndexedFiles
            .Join(tracks, r => r.MediaId, t => t.Id, (r, t) => new { r.LibraryId, t.AlbumId })
            .Join(albums.Where(a => a.ArtistId != null), x => x.AlbumId, a => a.Id, (x, a) => new { x.LibraryId, MediaId = a.ArtistId!.Value });

        var serieFromEpisodesIndexed = context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Join(episodes, f => f.MediaId!.Value, e => e.Id, (f, e) => new { f.LibraryId, MediaId = e.SerieId });

        var serieFromEpisodesRemote = context.RemoteIndexedFiles
            .Join(episodes, r => r.MediaId, e => e.Id, (r, e) => new { r.LibraryId, MediaId = e.SerieId });

        var seasonFromEpisodesIndexed = context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Join(episodes, f => f.MediaId!.Value, e => e.Id, (f, e) => new { f.LibraryId, MediaId = e.SeasonId });

        var seasonFromEpisodesRemote = context.RemoteIndexedFiles
            .Join(episodes, r => r.MediaId, e => e.Id, (r, e) => new { r.LibraryId, MediaId = e.SeasonId });

        return fromIndexed
            .Union(fromRemote)
            .Union(albumFromTracksIndexed)
            .Union(albumFromTracksRemote)
            .Union(artistFromAlbumsRemote)
            .Union(artistFromTracksIndexed)
            .Union(artistFromTracksRemote)
            .Union(serieFromEpisodesIndexed)
            .Union(serieFromEpisodesRemote)
            .Union(seasonFromEpisodesIndexed)
            .Union(seasonFromEpisodesRemote)
            .Select(p => new MediaLibraryPairProjection { LibraryId = p.LibraryId, MediaId = p.MediaId });
    }

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
