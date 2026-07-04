using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.UI.Helpers;

public static class LiteMediaThumbnailHelper
{
    public static MetadataPictureDto? ResolvePicture(LiteMediaDto item) =>
        LiteMediaPictureResolver.ResolvePicture(item);

    public enum ThumbShape
    {
        Square,
        Widescreen,
        Poster
    }

    public static ThumbShape GetThumbShape(LiteMediaDto item) =>
        item switch
        {
            LiteSerieEpisodeDto => ThumbShape.Widescreen,
            LiteSerieSeasonDto or LiteMovieDto or LiteSerieDto => ThumbShape.Poster,
            _ => ThumbShape.Square
        };

    public static (int Width, int Height) GetThumbSize(ThumbShape shape) =>
        shape switch
        {
            ThumbShape.Widescreen => (85, 48),
            ThumbShape.Poster => (32, 48),
            _ => (48, 48)
        };

    public static string GetShapeClass(ThumbShape shape) =>
        shape switch
        {
            ThumbShape.Widescreen => "library-list-item--widescreen",
            ThumbShape.Poster => "library-list-item--poster",
            _ => "library-list-item--square"
        };
}
