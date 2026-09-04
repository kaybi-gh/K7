using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;

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

    [Test]
    public void ResolveAdaptiveBackdropUrls_ShouldOmitCacheBuster_WhenVersionIsNull()
    {
        var picture = new MetadataPictureDto
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Backdrop,
            Uri = new Uri("/api/metadata-pictures/backdrop.jpg", UriKind.Relative),
            OriginalWidth = 3840,
            OriginalHeight = 2160
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var (displayUrl, highResUrl) = MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(picture, apiClient);

        displayUrl.Should().Be("https://localhost/api/metadata-pictures/backdrop.jpg?size=Medium");
        displayUrl.Should().NotContain("v=");
        highResUrl.Should().Be("https://localhost/api/metadata-pictures/backdrop.jpg");
    }

    [Test]
    public void ResolveAdaptiveBackdropUrls_ShouldKeepMediumOnly_WhenSourceFitsBudget()
    {
        var picture = new MetadataPictureDto
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Backdrop,
            Uri = new Uri("/api/metadata-pictures/backdrop.jpg", UriKind.Relative),
            OriginalWidth = 1920,
            OriginalHeight = 1080
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var (displayUrl, highResUrl) = MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(picture, apiClient);

        displayUrl.Should().Contain("size=Medium");
        highResUrl.Should().BeNull();
    }

    [Test]
    public void ResolveAdaptiveBackdropUrls_ShouldReturnNull_WhenPictureIsCover()
    {
        var cover = new MetadataPictureDto
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            Uri = new Uri("/api/metadata-pictures/cover.jpg", UriKind.Relative),
            OriginalWidth = 1400,
            OriginalHeight = 1400
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var (displayUrl, highResUrl) = MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(cover, apiClient);

        displayUrl.Should().BeNull();
        highResUrl.Should().BeNull();
        apiClient.DidNotReceive().GetAbsoluteUri(Arg.Any<string?>());
    }
}
