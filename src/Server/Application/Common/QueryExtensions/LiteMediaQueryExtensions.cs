using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.QueryExtensions;

public static class LiteMediaQueryExtensions
{
    public static IQueryable<BaseMedia> ApplyLiteMappingIncludes(this IQueryable<BaseMedia> query, Guid? userId)
    {
        query = query
            .Include(m => m.Pictures)
            .Include(m => m.Ratings)
            .Include(m => m.IndexedFiles)
                .ThenInclude(f => f.FileMetadata)
            .Include(m => m.RemoteIndexedFiles);

        if (userId.HasValue)
        {
            query = query.Include(m => m.UserMediaStates.Where(s => s.UserId == userId.Value));

            query = query
                .Include(m => ((SerieSeason)m).Episodes)
                    .ThenInclude(e => e.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        query = query
            .Include(m => ((MusicTrack)m).MetadataTags)
                .ThenInclude(mt => mt.MetadataTag)
            .Include(m => ((MusicTrack)m).Album!)
                .ThenInclude(a => a.Pictures)
            .Include(m => ((MusicTrack)m).Album!)
                .ThenInclude(a => a.Artist)
            .Include(m => ((MusicAlbum)m).Artist)
            .Include(m => ((MusicTrack)m).Artist)
            .Include(m => ((MusicTrack)m).AudioAnalysis)
            .Include(m => ((MusicTrack)m).ArtistCredits)
                .ThenInclude(c => c.MusicArtist)
            .Include(m => ((MusicArtist)m).Albums)
                .ThenInclude(a => a.Pictures)
            .Include(m => ((MusicArtist)m).ArtistCredits)
                .ThenInclude(c => c.Media)
                    .ThenInclude(m => ((MusicTrack)m).Album)
                        .ThenInclude(a => a.Pictures)
            .Include(m => ((SerieEpisode)m).Season!)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieEpisode)m).Serie!)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieSeason)m).Serie!)
                .ThenInclude(s => s.Pictures)
            .Include(m => ((SerieSeason)m).Episodes);

        return query;
    }
}
