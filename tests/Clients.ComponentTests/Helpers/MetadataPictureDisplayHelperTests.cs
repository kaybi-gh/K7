using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MetadataPictureDisplayHelperTests
{
    [Test]
    public void SizeFor_ShouldReturnSmall_WhenThumb()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Thumb)
            .Should().Be(MetadataPictureSize.Small);
    }

    [Test]
    public void SizeFor_ShouldReturnMedium_WhenCard()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Card)
            .Should().Be(MetadataPictureSize.Medium);
    }

    [Test]
    public void SizeFor_ShouldReturnNull_WhenHero()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Hero)
            .Should().BeNull();
    }

    [Test]
    public void SizeForHeroBackdrop_ShouldReturnMedium()
    {
        MetadataPictureDisplayHelper.SizeForHeroBackdrop()
            .Should().Be(MetadataPictureSize.Medium);
    }

    [Test]
    public void IsHdStill_ShouldReturnTrue_WhenDimensionsUnknown()
    {
        var still = new MetadataPictureDto { Type = MetadataPictureType.Still };

        MetadataPictureDisplayHelper.IsHdStill(still).Should().BeTrue();
    }

    [Test]
    public void IsHdStill_ShouldReturnTrue_WhenWidthMeetsThreshold()
    {
        var still = new MetadataPictureDto
        {
            Type = MetadataPictureType.Still,
            OriginalWidth = 1280,
            OriginalHeight = 720
        };

        MetadataPictureDisplayHelper.IsHdStill(still).Should().BeTrue();
    }

    [Test]
    public void IsHdStill_ShouldReturnFalse_WhenBelowThreshold()
    {
        var still = new MetadataPictureDto
        {
            Type = MetadataPictureType.Still,
            OriginalWidth = 640,
            OriginalHeight = 360
        };

        MetadataPictureDisplayHelper.IsHdStill(still).Should().BeFalse();
    }

    [Test]
    public void ShouldSoftenTvHeroBackdrop_ShouldReturnFalse_WhenEpisodeStillIsHd()
    {
        var still = new MetadataPictureDto
        {
            Type = MetadataPictureType.Still,
            OriginalWidth = 1920,
            OriginalHeight = 1080
        };

        MetadataPictureDisplayHelper.ShouldSoftenTvHeroBackdrop(MediaType.SerieEpisode, still)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldSoftenTvHeroBackdrop_ShouldReturnTrue_WhenEpisodeStillIsBelowHd()
    {
        var still = new MetadataPictureDto
        {
            Type = MetadataPictureType.Still,
            OriginalWidth = 640,
            OriginalHeight = 360
        };

        MetadataPictureDisplayHelper.ShouldSoftenTvHeroBackdrop(MediaType.SerieEpisode, still)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldSoftenTvHeroBackdrop_ShouldReturnFalse_WhenEpisodeUsesSerieBackdrop()
    {
        var backdrop = new MetadataPictureDto
        {
            Type = MetadataPictureType.Backdrop,
            OriginalWidth = 1920,
            OriginalHeight = 1080
        };

        MetadataPictureDisplayHelper.ShouldSoftenTvHeroBackdrop(MediaType.SerieEpisode, backdrop)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldSoftenTvHeroBackdrop_ShouldReturnTrue_WhenMusicCover()
    {
        var cover = new MetadataPictureDto
        {
            Type = MetadataPictureType.Cover,
            OriginalWidth = 1400,
            OriginalHeight = 1400
        };

        MetadataPictureDisplayHelper.ShouldSoftenTvHeroBackdrop(MediaType.MusicAlbum, cover)
            .Should().BeTrue();
    }
}
