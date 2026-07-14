using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.QueryExtensions;

public static class LiteMediaQueryExtensions
{
    public static IQueryable<BaseMedia> ApplyLiteMappingIncludes(this IQueryable<BaseMedia> query, Guid? userId)
    {
        query = query
            .IncludeMetadataTagsForMapping()
            .Include(m => m.Pictures)
            .Include(m => m.Ratings)
            .Include(m => m.IndexedFiles)
            .Include(m => m.RemoteIndexedFiles);

        if (userId.HasValue)
            query = query.Include(m => m.UserMediaStates.Where(s => s.UserId == userId.Value));

        query = query
            .Include(m => ((MusicTrack)m).IndexedFiles)
                .ThenInclude(f => f.FileMetadata)
            .Include(m => ((MusicTrack)m).Album!)
                .ThenInclude(a => a.Pictures)
            .Include(m => ((MusicTrack)m).Album!)
                .ThenInclude(a => a.Artist)
            .Include(m => ((MusicTrack)m).Artist)
            .Include(m => ((MusicTrack)m).AudioAnalysis)
            .Include(m => ((MusicTrack)m).ArtistCredits)
                .ThenInclude(c => c.MusicArtist)
            .Include(m => ((MusicAlbum)m).Artist)
            .Include(m => ((SerieEpisode)m).Season!)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieEpisode)m).Serie!)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieSeason)m).Serie!)
                .ThenInclude(s => s.Pictures);

        return query;
    }
}
