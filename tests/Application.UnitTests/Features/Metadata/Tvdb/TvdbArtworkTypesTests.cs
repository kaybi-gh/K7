using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbArtworkTypesTests
{
    [TestCase(TvdbArtworkTypes.Series.Banner, null)]
    [TestCase(TvdbArtworkTypes.Series.Icon, null)]
    [TestCase(TvdbArtworkTypes.Series.Background, MetadataPictureType.Backdrop)]
    [TestCase(TvdbArtworkTypes.Series.Poster, MetadataPictureType.Poster)]
    [TestCase(TvdbArtworkTypes.Series.ClearLogo, MetadataPictureType.Logo)]
    [TestCase(TvdbArtworkTypes.Season.Poster, MetadataPictureType.Poster)]
    [TestCase(TvdbArtworkTypes.Episode.Screencap16x9, MetadataPictureType.Still)]
    public void MapToPictureType_ShouldMapTvdbArtworkIds(int artworkTypeId, MetadataPictureType? expected) =>
        TvdbArtworkTypes.MapToPictureType(artworkTypeId).Should().Be(expected);
}
