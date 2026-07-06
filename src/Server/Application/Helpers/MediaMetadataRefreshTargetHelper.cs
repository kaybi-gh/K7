using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Helpers;

internal static class MediaMetadataRefreshTargetHelper
{
    internal static async Task<BaseMedia> ResolveRefreshMediaAsync(
        IApplicationDbContext context,
        BaseMedia media,
        CancellationToken cancellationToken = default) =>
        media switch
        {
            MusicTrack track => await context.Medias.OfType<MusicAlbum>()
                .Include(a => a.ExternalIds)
                .FirstAsync(a => a.Id == track.AlbumId, cancellationToken),

            SerieEpisode episode => await context.Medias.OfType<Serie>()
                .Include(s => s.ExternalIds)
                .FirstAsync(s => s.Id == episode.SerieId, cancellationToken),

            SerieSeason season => await context.Medias.OfType<Serie>()
                .Include(s => s.ExternalIds)
                .FirstAsync(s => s.Id == season.SerieId, cancellationToken),

            _ => media
        };
}
