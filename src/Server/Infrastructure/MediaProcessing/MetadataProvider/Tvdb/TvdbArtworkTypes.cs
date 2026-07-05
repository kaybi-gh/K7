using K7.Server.Domain.Enums;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

// TVDB v4 artwork type IDs from GET /artwork/types (recordType-specific).
internal static class TvdbArtworkTypes
{
    internal static class Series
    {
        internal const int Banner = 1;
        internal const int Poster = 2;
        internal const int Background = 3;
        internal const int Icon = 5;
        internal const int ClearArt = 22;
        internal const int ClearLogo = 23;
    }

    internal static class Season
    {
        internal const int Banner = 6;
        internal const int Poster = 7;
        internal const int Background = 8;
    }

    internal static class Episode
    {
        internal const int Screencap16x9 = 11;
        internal const int Screencap4x3 = 12;
    }

    internal static MetadataPictureType? MapToPictureType(int artworkTypeId) => artworkTypeId switch
    {
        Series.Background or Season.Background => MetadataPictureType.Backdrop,
        Series.Poster or Season.Poster => MetadataPictureType.Poster,
        Series.ClearLogo => MetadataPictureType.Logo,
        Series.ClearArt => MetadataPictureType.Logo,
        Episode.Screencap16x9 or Episode.Screencap4x3 => MetadataPictureType.Still,
        _ => null
    };
}
