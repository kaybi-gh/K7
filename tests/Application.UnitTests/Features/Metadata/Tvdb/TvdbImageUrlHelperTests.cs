using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbImageUrlHelperTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BuildImageUrl_ShouldReturnNull_WhenPathIsMissing(string? imagePath) =>
        TvdbImageUrlHelper.BuildImageUrl(imagePath).Should().BeNull();

    [Test]
    public void BuildImageUrl_ShouldReturnAbsoluteUrl_WhenPathIsAlreadyAbsolute()
    {
        const string url = "https://artworks.thetvdb.com/banners/v4/actor/265444/photo/6a345175c5cf8.jpg";

        TvdbImageUrlHelper.BuildImageUrl(url).Should().Be(url);
    }

    [TestCase(
        "episodes/357888/7069846.jpg",
        "https://artworks.thetvdb.com/banners/episodes/357888/7069846.jpg")]
    [TestCase(
        "/episodes/357888/7069846.jpg",
        "https://artworks.thetvdb.com/banners/episodes/357888/7069846.jpg")]
    [TestCase(
        "series/102621/episodes/5ef56513ccb17.jpg",
        "https://artworks.thetvdb.com/banners/series/102621/episodes/5ef56513ccb17.jpg")]
    public void BuildImageUrl_ShouldPrefixBannersPath_WhenRelativePathOmitsBannersSegment(
        string imagePath,
        string expected) =>
        TvdbImageUrlHelper.BuildImageUrl(imagePath).Should().Be(expected);

    [TestCase(
        "banners/v4/actor/265444/photo/6a345175c5cf8.jpg",
        "https://artworks.thetvdb.com/banners/v4/actor/265444/photo/6a345175c5cf8.jpg")]
    [TestCase(
        "/banners/v4/actor/265444/photo/6a345175c5cf8.jpg",
        "https://artworks.thetvdb.com/banners/v4/actor/265444/photo/6a345175c5cf8.jpg")]
    [TestCase(
        "banners/person/8194548/60f5ac1ba12cc.jpg",
        "https://artworks.thetvdb.com/banners/person/8194548/60f5ac1ba12cc.jpg")]
    public void BuildImageUrl_ShouldNotDuplicateBannersSegment_WhenRelativePathIncludesIt(
        string imagePath,
        string expected) =>
        TvdbImageUrlHelper.BuildImageUrl(imagePath).Should().Be(expected);
}
