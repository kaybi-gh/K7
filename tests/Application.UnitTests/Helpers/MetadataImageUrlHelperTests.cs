using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataImageUrlHelperTests
{
    [Test]
    public void IsVectorImageUrl_ShouldReturnTrue_WhenUrlEndsWithSvg()
    {
        MetadataImageUrlHelper.IsVectorImageUrl("https://commons.wikimedia.org/wiki/Special:FilePath/Logo.svg")
            .Should().BeTrue();
    }

    [Test]
    public void TryCreateRemoteUri_ShouldAcceptSvgUrls()
    {
        MetadataImageUrlHelper.TryCreateRemoteUri("https://example.com/logo.svg", out var uri).Should().BeTrue();
        uri!.OriginalString.Should().Be("https://example.com/logo.svg");
    }

    [Test]
    public void BuildWikimediaCommonsImageUrl_ShouldEncodeFilenameWithoutRasterizing()
    {
        var url = MetadataImageUrlHelper.BuildWikimediaCommonsImageUrl("Some Artist.svg");

        url.Should().Be("https://commons.wikimedia.org/wiki/Special:FilePath/Some%20Artist.svg");
    }

    [Test]
    public void BuildWikimediaThumbnailUrl_ShouldRasterizeCommonsSvg_ForPickerPreview()
    {
        var thumb = MetadataImageUrlHelper.BuildWikimediaThumbnailUrl(
            "https://commons.wikimedia.org/wiki/Special:FilePath/Artist.svg");

        thumb.Should().Be("https://commons.wikimedia.org/wiki/Special:FilePath/Artist.svg?width=300");
    }

    [Test]
    public void FilterProviderImages_ShouldKeepSvgImages()
    {
        var images = new List<ProviderImageDto>
        {
            new()
            {
                Url = "https://example.com/logo.svg",
                ThumbnailUrl = "https://example.com/logo.svg",
                Type = MetadataPictureType.Logo
            },
            new()
            {
                Url = "https://coverartarchive.org/front-500.jpg",
                ThumbnailUrl = "https://coverartarchive.org/front-250.jpg",
                Type = MetadataPictureType.Cover
            }
        };

        MetadataImageUrlHelper.FilterProviderImages(images).Should().HaveCount(2);
    }

    [Test]
    public void GetExtensionFromContentType_ShouldMapKnownImageTypes()
    {
        MetadataImageUrlHelper.GetExtensionFromContentType("image/png; charset=binary")
            .Should().Be(".png");
        MetadataImageUrlHelper.IsVectorContentType("image/svg+xml").Should().BeTrue();
    }

    [Test]
    public void FilterHdEpisodeStills_ShouldExcludeLowResolutionStills()
    {
        var images = new List<ProviderImageDto>
        {
            new()
            {
                Url = "https://tvdb.example/still.jpg",
                ThumbnailUrl = "https://tvdb.example/still.jpg",
                Type = MetadataPictureType.Still,
                Provider = "tvdb",
                Width = 640,
                Height = 360
            },
            new()
            {
                Url = "https://tmdb.example/still.jpg",
                ThumbnailUrl = "https://tmdb.example/still.jpg",
                Type = MetadataPictureType.Still,
                Provider = "tmdb",
                Width = 1920,
                Height = 1080,
                VoteAverage = 5
            }
        };

        var filtered = MetadataImageUrlHelper.FilterHdEpisodeStills(images);

        filtered.Should().ContainSingle();
        filtered[0].Provider.Should().Be("tmdb");
    }
}
