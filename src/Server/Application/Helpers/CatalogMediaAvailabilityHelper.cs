using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Helpers;

internal static class CatalogMediaAvailabilityHelper
{
    internal static IQueryable<BaseMedia> WhereHasPlayableFiles(IQueryable<BaseMedia> query) =>
        query.Where(x =>
            x is MusicAlbum
                ? x.RemoteIndexedFiles.Any()
                    || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any() || t.RemoteIndexedFiles.Any())
                : x is Serie
                    ? x.RemoteIndexedFiles.Any()
                        || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any() || e.RemoteIndexedFiles.Any()))
                    : x.IndexedFiles.Any() || x.RemoteIndexedFiles.Any());
}
