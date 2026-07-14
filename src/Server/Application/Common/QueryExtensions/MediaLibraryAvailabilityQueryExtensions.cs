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
}
