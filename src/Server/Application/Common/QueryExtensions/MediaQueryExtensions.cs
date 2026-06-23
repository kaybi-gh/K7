using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.QueryExtensions;

public static class MediaQueryExtensions
{
    public static IQueryable<BaseMedia> IncludeMetadataTagsForMapping(this IQueryable<BaseMedia> query) =>
        query
            .Include(m => m.MetadataTags)
                .ThenInclude(mt => mt.MetadataTag)
            .Include(m => ((MusicTrack)m).Album)
                .ThenInclude(a => a!.MetadataTags)
                    .ThenInclude(mt => mt.MetadataTag)
            .Include(m => ((SerieSeason)m).Serie)
                .ThenInclude(s => s!.MetadataTags)
                    .ThenInclude(mt => mt.MetadataTag)
            .Include(m => ((SerieEpisode)m).Serie)
                .ThenInclude(s => s!.MetadataTags)
                    .ThenInclude(mt => mt.MetadataTag)
            .Include(m => ((MusicArtist)m).Albums)
                .ThenInclude(a => a.MetadataTags)
                    .ThenInclude(mt => mt.MetadataTag);
}
