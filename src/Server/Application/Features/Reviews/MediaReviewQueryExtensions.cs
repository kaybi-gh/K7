using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Reviews;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews;

internal static class MediaReviewQueryExtensions
{
    internal static IQueryable<MediaReview> IncludeReviewMediaDetails(this IQueryable<MediaReview> query) =>
        query
            .Include(r => r.UserRating)
            .Include(r => r.Media!)
                .ThenInclude(m => m.ExternalIds)
            .Include(r => r.Media!)
                .ThenInclude(m => m.Pictures)
            .Include(r => r.Media!)
                .ThenInclude(m => ((MusicTrack)m).Album)
                .ThenInclude(a => a.Pictures)
            .Include(r => r.Media!)
                .ThenInclude(m => ((SerieEpisode)m).Season)
                .ThenInclude(s => s.Pictures)
            .Include(r => r.Media!)
                .ThenInclude(m => ((SerieEpisode)m).Serie)
                .ThenInclude(s => s.Pictures)
            .Include(r => r.Media!)
                .ThenInclude(m => ((SerieSeason)m).Serie)
                .ThenInclude(s => s.Pictures);
}
