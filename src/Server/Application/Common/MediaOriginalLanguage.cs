using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common;

public static class MediaOriginalLanguage
{
    public static async Task<string?> ResolveAsync(
        IApplicationDbContext context,
        Guid? mediaId,
        CancellationToken cancellationToken = default)
    {
        if (mediaId is null)
            return null;

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId.Value, cancellationToken);

        return media switch
        {
            Movie movie => movie.OriginalLanguage,
            Serie serie => serie.OriginalLanguage,
            SerieEpisode episode => await context.Medias
                .OfType<Serie>()
                .AsNoTracking()
                .Where(s => s.Id == episode.SerieId)
                .Select(s => s.OriginalLanguage)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }
}
