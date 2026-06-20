using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common.QueryExtensions;

public static class UserMediaExclusionQueryExtensions
{
    public static IQueryable<BaseMedia> WhereNotUserExcluded(
        this IQueryable<BaseMedia> query,
        IQueryable<Guid> excludedMediaIds)
    {
        return query.Where(x =>
            !excludedMediaIds.Contains(x.Id)
            && !(x is SerieEpisode && excludedMediaIds.Contains(((SerieEpisode)x).SerieId))
            && !(x is SerieSeason && excludedMediaIds.Contains(((SerieSeason)x).SerieId))
            && !(x is MusicTrack && excludedMediaIds.Contains(((MusicTrack)x).AlbumId)));
    }
}
