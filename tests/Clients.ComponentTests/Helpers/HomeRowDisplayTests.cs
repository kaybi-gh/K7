using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HomeRowDisplayTests
{
    private static MediaCardViewModel Card(
        string id,
        MediaCardKind kind = MediaCardKind.Poster,
        bool backdrop = true,
        MediaType type = MediaType.Movie) => new()
        {
            Id = id,
            Kind = kind,
            MediaType = type,
            Title = $"Item {id}",
            BackdropUrl = backdrop ? $"https://img.test/{id}.jpg" : null
        };

    [Test]
    public void Items_ShouldDropDuplicates()
    {
        var items = HomeRowDisplay.Items([Card("1"), Card("1"), Card("2")]);

        items.Select(i => i.Id).Should().Equal("1", "2");
    }

    [TestCase(MediaCardKind.Poster, MediaCardVariant.Poster)]
    [TestCase(MediaCardKind.Serie, MediaCardVariant.Poster)]
    [TestCase(MediaCardKind.Episode, MediaCardVariant.Poster)]
    [TestCase(MediaCardKind.Cover, MediaCardVariant.Cover)]
    public void Variant_ShouldFollowTheItem_WhenRowIsNotLandscape(MediaCardKind kind, MediaCardVariant expected)
    {
        HomeRowDisplay.Variant(HomeRowDisplayType.Carousel, Card("1", kind)).Should().Be(expected);
        HomeRowDisplay.Variant(HomeRowDisplayType.Featured, Card("1", kind)).Should().Be(expected);
    }

    [TestCase(MediaCardKind.Poster)]
    [TestCase(MediaCardKind.Cover)]
    public void Variant_ShouldBeWideForEveryItem_WhenRowIsLandscape(MediaCardKind kind)
    {
        HomeRowDisplay.Variant(HomeRowDisplayType.Landscape, Card("1", kind)).Should().Be(MediaCardVariant.Backdrop);
    }

    [TestCase(HomeRowDisplayType.Carousel)]
    [TestCase(HomeRowDisplayType.Landscape)]
    public void Featured_ShouldBeEmpty_WhenRowIsNotFeatured(HomeRowDisplayType displayType)
    {
        HomeRowDisplay.Featured(displayType, [Card("1")]).Should().BeEmpty();
    }

    [TestCase(HomeRowDisplayType.Featured)]
    [TestCase(HomeRowDisplayType.FeaturedBleed)]
    public void Featured_ShouldPickTitles_ForBothBannerStyles(HomeRowDisplayType displayType)
    {
        HomeRowDisplay.Featured(displayType, [Card("1"), Card("2")]).Select(i => i.Id).Should().Equal("1", "2");
    }

    [TestCase(HomeRowDisplayType.Carousel, false)]
    [TestCase(HomeRowDisplayType.Landscape, false)]
    [TestCase(HomeRowDisplayType.Featured, true)]
    [TestCase(HomeRowDisplayType.FeaturedBleed, true)]
    public void IsBanner_ShouldTellTheTwoFamiliesApart(HomeRowDisplayType displayType, bool expected)
    {
        displayType.IsBanner().Should().Be(expected);
    }

    [Test]
    public void Featured_ShouldSkipItemsWithoutBackdropAndMusic()
    {
        var featured = HomeRowDisplay.Featured(
            HomeRowDisplayType.Featured,
            [Card("bare", backdrop: false), Card("album", MediaCardKind.Cover, type: MediaType.MusicAlbum), Card("movie")]);

        featured.Select(i => i.Id).Should().Equal("movie");
    }

    [TestCase(3, 3)]
    [TestCase(10, 10)]
    [TestCase(50, HomeRowDisplay.MaxBannerCount)]
    [TestCase(0, 1)]
    public void Featured_ShouldKeepAsManyDistinctItemsAsTheRowAsksFor_WithinBounds(int asked, int expected)
    {
        var cards = Enumerable.Range(1, 30).Select(i => Card(i.ToString())).Prepend(Card("1"));

        var featured = HomeRowDisplay.Featured(HomeRowDisplayType.Featured, cards, asked);

        featured.Should().HaveCount(expected);
        featured.Select(i => i.Id).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Featured_ShouldBeEmpty_WhenNothingHasABackdrop()
    {
        HomeRowDisplay.Featured(HomeRowDisplayType.Featured, [Card("bare", backdrop: false)]).Should().BeEmpty();
    }
}
