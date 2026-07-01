using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common.Services;

public static class EpisodePictureResolver
{
    public static IList<MetadataPicture>? ResolveDisplayPictures(SerieEpisode episode)
    {
        if (episode.Serie?.Pictures is { Count: > 0 } seriePictures)
            return seriePictures;

        if (episode.Season?.Pictures is { Count: > 0 } seasonPictures)
            return seasonPictures;

        if (episode.Pictures is { Count: > 0 } episodePictures)
            return episodePictures;

        return null;
    }
}
