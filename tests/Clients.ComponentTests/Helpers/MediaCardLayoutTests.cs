using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaCardLayoutTests
{
    [Test]
    public void VariantForBrowseMediaType_ShouldUsePoster_ForMoviesAndSeries()
    {
        MediaCardLayout.VariantForBrowseMediaType(MediaType.Movie).Should().Be(MediaCardVariant.Poster);
        MediaCardLayout.VariantForBrowseMediaType(MediaType.Serie).Should().Be(MediaCardVariant.Poster);
        MediaCardLayout.VariantForBrowseMediaType(MediaType.SerieSeason).Should().Be(MediaCardVariant.Poster);
    }

    [Test]
    public void VariantForBrowseMediaType_ShouldUseBackdrop_ForEpisodes()
    {
        MediaCardLayout.VariantForBrowseMediaType(MediaType.SerieEpisode).Should().Be(MediaCardVariant.Backdrop);
    }

    [Test]
    public void VariantForBrowseMediaType_ShouldUseCover_ForMusic()
    {
        MediaCardLayout.VariantForBrowseMediaType(MediaType.MusicAlbum).Should().Be(MediaCardVariant.Cover);
        MediaCardLayout.VariantForBrowseMediaType(MediaType.MusicArtist).Should().Be(MediaCardVariant.Cover);
        MediaCardLayout.VariantForBrowseMediaType(MediaType.MusicTrack).Should().Be(MediaCardVariant.Cover);
    }

    [Test]
    public void CssRatio_ShouldMatchMediaCardFormFactors()
    {
        MediaCardLayout.CssRatio(MediaCardVariant.Poster).Should().Be("2 / 3");
        MediaCardLayout.CssRatio(MediaCardVariant.Cover).Should().Be("1 / 1");
        MediaCardLayout.CssRatio(MediaCardVariant.Backdrop).Should().Be("16 / 9");
    }

    [Test]
    public void GridAspectRatio_ShouldBeHeightOverWidth()
    {
        MediaCardLayout.GridAspectRatio(MediaCardVariant.Poster).Should().Be(1.5f);
        MediaCardLayout.GridAspectRatio(MediaCardVariant.Cover).Should().Be(1f);
        MediaCardLayout.GridAspectRatio(MediaCardVariant.Backdrop).Should().Be(9f / 16f);
    }

    [Test]
    public void GridItemWidth_ShouldMatchPosterHeight_ForStills()
    {
        MediaCardLayout.GridItemWidth(MediaCardVariant.Poster).Should().Be(160);
        MediaCardLayout.GridItemWidth(MediaCardVariant.Cover).Should().Be(160);
        MediaCardLayout.GridItemWidth(MediaCardVariant.Backdrop).Should().Be(427);
    }
}
