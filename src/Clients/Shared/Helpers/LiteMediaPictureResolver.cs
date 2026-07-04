using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Helpers;

public static class LiteMediaPictureResolver
{
    public static MetadataPictureDto? ResolvePicture(LiteMediaDto item) =>
        item switch
        {
            LiteSerieEpisodeDto episode => ResolveEpisodePicture(episode),
            LiteSerieSeasonDto season => ResolveSeasonPicture(season),
            _ => ResolveDefaultPicture(item.Pictures)
        };

    public static MetadataPictureDto? ResolveEpisodePictures(IReadOnlyList<MetadataPictureDto>? pictures) =>
        ResolveByTypePriority(pictures, MetadataPictureType.Still, MetadataPictureType.Poster);

    public static MetadataPictureDto? ResolveEpisodeStill(LiteSerieEpisodeDto episode) =>
        episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still);

    private static MetadataPictureDto? ResolveEpisodePicture(LiteSerieEpisodeDto episode) =>
        ResolveByTypePriority(episode.Pictures, MetadataPictureType.Still, MetadataPictureType.Poster)
        ?? ResolveByTypePriority(episode.SeasonPictures, MetadataPictureType.Poster, MetadataPictureType.Still)
        ?? ResolveByTypePriority(episode.SeriePictures, MetadataPictureType.Poster, MetadataPictureType.Still);

    private static MetadataPictureDto? ResolveSeasonPicture(LiteSerieSeasonDto season) =>
        season.Poster
        ?? ResolveByTypePriority(season.Pictures, MetadataPictureType.Poster, MetadataPictureType.Still)
        ?? ResolveByTypePriority(season.SeriePictures, MetadataPictureType.Poster, MetadataPictureType.Still);

    private static MetadataPictureDto? ResolveDefaultPicture(IReadOnlyList<MetadataPictureDto>? pictures) =>
        ResolveByTypePriority(pictures, MetadataPictureType.Cover, MetadataPictureType.Poster, MetadataPictureType.Still);

    private static MetadataPictureDto? ResolveByTypePriority(
        IReadOnlyList<MetadataPictureDto>? pictures,
        params MetadataPictureType[] types)
    {
        if (pictures is null or { Count: 0 })
            return null;

        foreach (var type in types)
        {
            var match = pictures.FirstOrDefault(p => p.Type == type);
            if (match is not null)
                return match;
        }

        return pictures.FirstOrDefault();
    }
}
