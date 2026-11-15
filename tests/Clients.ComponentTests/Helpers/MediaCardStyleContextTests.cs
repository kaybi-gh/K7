using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaCardStyleContextTests
{
    private static MediaCardPreferencesDto Preferences(CardHoverStyle hover = CardHoverStyle.Zoom) =>
        new() { Default = { Hover = hover } };

    [TestCase(MediaType.Movie, CardMediaGroup.Movies)]
    [TestCase(MediaType.Serie, CardMediaGroup.Series)]
    [TestCase(MediaType.SerieSeason, CardMediaGroup.Series)]
    [TestCase(MediaType.SerieEpisode, CardMediaGroup.Series)]
    [TestCase(MediaType.MusicAlbum, CardMediaGroup.Music)]
    [TestCase(MediaType.MusicTrack, CardMediaGroup.Music)]
    public void GroupOf_ShouldFollowTheMediaType(MediaType type, CardMediaGroup expected)
    {
        MediaCardStyleContext.GroupOf(new MediaCardViewModel { Id = "1", MediaType = type }).Should().Be(expected);
    }

    [TestCase(MediaCardKind.Cover, CardMediaGroup.Music)]
    [TestCase(MediaCardKind.Episode, CardMediaGroup.Series)]
    public void GroupOf_ShouldFallBackToTheKind_WhenMediaTypeIsUnknown(MediaCardKind kind, CardMediaGroup expected)
    {
        MediaCardStyleContext.GroupOf(new MediaCardViewModel { Id = "1", Kind = kind }).Should().Be(expected);
    }

    [Test]
    public void GroupOf_ShouldBeNull_WhenCardIsNoMovieSeriesOrMusic()
    {
        MediaCardStyleContext.GroupOf(new MediaCardViewModel { Id = "1" }).Should().BeNull();
        MediaCardStyleContext.GroupOf(null).Should().BeNull();
    }

    [Test]
    public void Resolve_ShouldUseTheStyleOfTheKindOfMedia_WhenItHasOne()
    {
        var preferences = Preferences();
        preferences.Overrides[CardMediaGroup.Music] = new MediaCardStyleDto { ShowSubtitle = false };
        var context = new MediaCardStyleContext(preferences);

        var album = new MediaCardViewModel { Id = "1", MediaType = MediaType.MusicAlbum };
        var movie = new MediaCardViewModel { Id = "2", MediaType = MediaType.Movie };

        MediaCardStyleContext.Resolve(context, album).ShowSubtitle.Should().BeFalse();
        MediaCardStyleContext.Resolve(context, movie).Hover.Should().Be(CardHoverStyle.Zoom);
    }

    [Test]
    public void Resolve_ShouldGiveTheDefaults_WhenThereIsNoContext()
    {
        MediaCardStyleContext.Resolve(null, new MediaCardViewModel { Id = "1" }).IsDefault.Should().BeTrue();
    }

    [Test]
    public void Next_ShouldBeNull_WhenThereIsNothingToApply()
    {
        MediaCardStyleContext.Next(null, new MediaCardPreferencesDto()).Should().BeNull();
        MediaCardStyleContext.Next(null, null).Should().BeNull();
    }

    [Test]
    public void Next_ShouldKeepTheSameContext_WhenNothingAboutCardsChanged()
    {
        var current = MediaCardStyleContext.Next(null, Preferences());

        MediaCardStyleContext.Next(current, Preferences()).Should().BeSameAs(current);
    }

    [Test]
    public void Next_ShouldGiveANewContext_WhenPreferencesWereEditedInPlace()
    {
        var preferences = Preferences();
        var current = MediaCardStyleContext.Next(null, preferences);

        preferences.Default.Details = [CardDetailField.Rating];
        var next = MediaCardStyleContext.Next(current, preferences);

        next.Should().NotBeSameAs(current);
        next!.NeedsDetailedFeed.Should().BeTrue();
    }

    [Test]
    public void Next_ShouldGiveANewContext_WhenAKindOfMediaGetsItsOwnStyle()
    {
        var preferences = Preferences();
        var current = MediaCardStyleContext.Next(null, preferences);

        preferences.Overrides[CardMediaGroup.Series] = new MediaCardStyleDto { ShowTitle = false };

        MediaCardStyleContext.Next(current, preferences).Should().NotBeSameAs(current);
    }

    [Test]
    public void ShowsDetailsBelow_ShouldBeTrue_OnlyWhenAStylePutsDetailsUnderTheCard()
    {
        new MediaCardStyleContext(new MediaCardPreferencesDto()).ShowsDetailsBelow.Should().BeFalse();
        new MediaCardStyleContext(new MediaCardPreferencesDto
        {
            Default = { Details = [CardDetailField.Year], DetailsPlacement = CardDetailPlacement.Overlay }
        }).ShowsDetailsBelow.Should().BeFalse();
        new MediaCardStyleContext(new MediaCardPreferencesDto
        {
            Overrides = { [CardMediaGroup.Movies] = new MediaCardStyleDto { Details = [CardDetailField.Year] } }
        }).ShowsDetailsBelow.Should().BeTrue();
    }
}
