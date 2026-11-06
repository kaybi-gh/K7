using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common.QueryExtensions;

public static class MediaLibraryAvailabilityQueryExtensions
{
    public static IQueryable<TMedia> WhereHasLibraryAvailability<TMedia>(
        this IQueryable<TMedia> query,
        IApplicationDbContext context)
        where TMedia : BaseMedia =>
        query.Where(m => context.MediaLibraryAvailabilities.Any(a => a.MediaId == m.Id));

    public static IQueryable<TMedia> WhereAvailableInLibraries<TMedia>(
        this IQueryable<TMedia> query,
        IApplicationDbContext context,
        IReadOnlyList<Guid> libraryIds)
        where TMedia : BaseMedia =>
        libraryIds.Count == 0
            ? query
            : query.Where(m => context.MediaLibraryAvailabilities.Any(a =>
                a.MediaId == m.Id && libraryIds.Contains(a.LibraryId)));

    public static IQueryable<TMedia> WhereAvailableOutsideExcludedLibraries<TMedia>(
        this IQueryable<TMedia> query,
        IApplicationDbContext context,
        IQueryable<Guid> excludedLibraryIds)
        where TMedia : BaseMedia =>
        query.Where(m => context.MediaLibraryAvailabilities.Any(a =>
            a.MediaId == m.Id && !excludedLibraryIds.Contains(a.LibraryId)));

    public static IQueryable<TMedia> WhereLinkedToLibrary<TMedia>(
        this IQueryable<TMedia> query,
        IApplicationDbContext context,
        Guid libraryId)
        where TMedia : BaseMedia =>
        query.Where(m => context.MediaLibraryAvailabilities.Any(a =>
            a.MediaId == m.Id && a.LibraryId == libraryId));

    /// <summary>
    /// Keeps media the user can see despite library exclusions.
    /// A row in an allowed library counts, and so does a file on the media or on a child
    /// track or episode. An album, artist, series or season with no files of its own is
    /// not visible when every child file sits in an excluded library.
    /// Metadata with no library row, no files and no children stays visible.
    /// </summary>
    public static IQueryable<TMedia> WhereLibraryVisibleToUser<TMedia>(
        this IQueryable<TMedia> query,
        IApplicationDbContext context,
        IQueryable<Guid> excludedLibraryIds)
        where TMedia : BaseMedia =>
        query.Where(m =>
            context.MediaLibraryAvailabilities.Any(a =>
                a.MediaId == m.Id && !excludedLibraryIds.Contains(a.LibraryId))
            || m.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
            || m.RemoteIndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
            || context.Medias.OfType<MusicTrack>().Any(t =>
                (t.AlbumId == m.Id
                    || t.ArtistId == m.Id
                    || t.Album.ArtistId == m.Id
                    || t.ArtistCredits.Any(c => c.MusicArtistId == m.Id)
                    || t.Album.ArtistCredits.Any(c => c.MusicArtistId == m.Id))
                && (t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                    || t.RemoteIndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))))
            || context.Medias.OfType<SerieEpisode>().Any(e =>
                (e.SerieId == m.Id || e.SeasonId == m.Id)
                && (e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                    || e.RemoteIndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))))
            || (!context.MediaLibraryAvailabilities.Any(a => a.MediaId == m.Id)
                && !m.IndexedFiles.Any()
                && !m.RemoteIndexedFiles.Any()
                && !context.Medias.OfType<MusicTrack>().Any(t =>
                    t.AlbumId == m.Id
                    || t.ArtistId == m.Id
                    || t.Album.ArtistId == m.Id
                    || t.ArtistCredits.Any(c => c.MusicArtistId == m.Id)
                    || t.Album.ArtistCredits.Any(c => c.MusicArtistId == m.Id))
                && !context.Medias.OfType<SerieEpisode>().Any(e =>
                    e.SerieId == m.Id || e.SeasonId == m.Id)));
}
