using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class SerieEpisodeEnrichmentHelper
{
    public static bool IsUnenriched(SerieEpisode episode)
    {
        var placeholderTitle = string.IsNullOrWhiteSpace(episode.Title)
            || episode.Title.Equals($"Episode {episode.EpisodeNumber}", StringComparison.OrdinalIgnoreCase);

        if (!placeholderTitle)
            return false;

        return string.IsNullOrWhiteSpace(episode.Overview)
            && episode.ExternalIds.Count == 0
            && !episode.Pictures.Any(picture => picture.Type == MetadataPictureType.Still);
    }

    public static bool IsSeasonUnenriched(SerieSeason season)
    {
        if (season.Pictures.Count > 0 || !string.IsNullOrWhiteSpace(season.Overview))
            return false;

        var placeholder = season.SeasonNumber == 0
            ? "Specials"
            : $"Season {season.SeasonNumber}";

        return string.IsNullOrWhiteSpace(season.Title)
            || season.Title.Equals(placeholder, StringComparison.OrdinalIgnoreCase);
    }

    public static void RemoveExistingPictureTypes(BaseMedia media, IList<MetadataPicture>? incomingPictures)
    {
        if (incomingPictures is null || incomingPictures.Count == 0)
            return;

        for (var i = incomingPictures.Count - 1; i >= 0; i--)
        {
            var type = incomingPictures[i].Type;
            if (media.Pictures.Any(picture => picture.Type == type))
                incomingPictures.RemoveAt(i);
        }
    }
}
