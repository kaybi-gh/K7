using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

public static class MetadataPictureSizesHelper
{
    public static IEnumerable<Guid> ExtractPictureIdsFromMedias(IEnumerable<BaseMedia> medias)
    {
        foreach (var media in medias)
        {
            foreach (var pictureId in media.Pictures.Select(p => p.Id))
                yield return pictureId;

            switch (media)
            {
                case SerieEpisode episode:
                    if (episode.Serie?.Pictures is { } seriePictures)
                    {
                        foreach (var pictureId in seriePictures.Select(p => p.Id))
                            yield return pictureId;
                    }

                    if (episode.Season?.Pictures is { } seasonPictures)
                    {
                        foreach (var pictureId in seasonPictures.Select(p => p.Id))
                            yield return pictureId;
                    }

                    break;
                case MusicTrack track when track.Album?.Pictures is { } albumPictures:
                    foreach (var pictureId in albumPictures.Select(p => p.Id))
                        yield return pictureId;
                    break;
            }
        }
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>> GetAvailableSizesByPictureIdsAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> pictureIds,
        CancellationToken cancellationToken = default)
    {
        var ids = pictureIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, IReadOnlyList<MetadataPictureSize>>();

        var rows = await context.MetadataPictureVariants
            .AsNoTracking()
            .Where(v => ids.Contains(v.MetadataPictureId))
            .Select(v => new { v.MetadataPictureId, v.Size })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.MetadataPictureId)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<MetadataPictureSize> (g) => g.Select(x => x.Size).Distinct().ToList());
    }
}
