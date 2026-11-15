using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class MediaCardStylesTests
{
    private static MediaCardPreferencesDto Cards() => new()
    {
        Default =
        {
            ShowSubtitle = false,
            Details = [CardDetailField.Year, CardDetailField.Rating],
            DetailsPlacement = CardDetailPlacement.Hover,
            Hover = CardHoverStyle.Lift
        },
        Overrides = { [CardMediaGroup.Music] = new MediaCardStyleDto { ShowTitle = false } }
    };

    [Test]
    public void Parse_ShouldReturnWhatSerializeWrote()
    {
        var parsed = MediaCardStyles.Parse(MediaCardStyles.Serialize(Cards()));

        parsed.Should().BeEquivalentTo(Cards());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{\"default\":{\"hover\":99}}")]
    public void Parse_ShouldReturnNothing_WhenStoredValueIsMissingOrNotValid(string? json)
    {
        MediaCardStyles.Parse(json).Should().BeNull();
    }

    [Test]
    public void IsValid_ShouldFail_WhenAStyleIsBroken()
    {
        MediaCardStyles.IsValid((MediaCardPreferencesDto?)null).Should().BeFalse();
        MediaCardStyles.IsValid(new MediaCardPreferencesDto { Default = { Details = null! } }).Should().BeFalse();
        MediaCardStyles.IsValid(new MediaCardPreferencesDto { Default = { Details = [(CardDetailField)99] } }).Should().BeFalse();
        MediaCardStyles.IsValid(new MediaCardPreferencesDto
        {
            Default = { Details = [CardDetailField.Year, CardDetailField.Year] }
        }).Should().BeFalse();
        MediaCardStyles.IsValid(new MediaCardPreferencesDto { Default = { DetailsPlacement = (CardDetailPlacement)99 } })
            .Should().BeFalse();
        MediaCardStyles.IsValid(new MediaCardPreferencesDto
        {
            Overrides = { [(CardMediaGroup)99] = new MediaCardStyleDto() }
        }).Should().BeFalse();
    }

    [Test]
    public void Copy_ShouldNotShareAnythingWithTheOriginal()
    {
        var original = Cards();

        var copy = MediaCardStyles.Copy(original);
        copy.Default.Details.Add(CardDetailField.Genre);
        copy.Overrides[CardMediaGroup.Music].ShowTitle = true;

        original.Default.Details.Should().Equal(CardDetailField.Year, CardDetailField.Rating);
        original.Overrides[CardMediaGroup.Music].ShowTitle.Should().BeFalse();
    }

    [Test]
    public void EffectiveCards_ShouldBeTheServerDefault_WhenTheUserHasNoStyle()
    {
        var service = new ThemeService();
        var serverDefault = Cards();
        service.SetServerDefaultCards(serverDefault);

        service.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);

        service.EffectiveCards.Should().BeSameAs(serverDefault);
    }

    [Test]
    public void EffectiveCards_ShouldBeTheUsersStyle_WhenTheyHaveOne()
    {
        var service = new ThemeService();
        service.SetServerDefaultCards(Cards());
        var own = new MediaCardPreferencesDto { Default = { Hover = CardHoverStyle.Zoom } };

        service.Apply(new AppearancePreferencesDto { Cards = own }, DeviceType.Desktop);

        service.EffectiveCards.Should().BeSameAs(own);
    }

    [Test]
    public void SetServerDefaultCards_ShouldNotify_OnceTheAppearanceIsKnown()
    {
        var service = new ThemeService();
        var changes = 0;
        service.ThemeOnChange += () => changes++;

        service.SetServerDefaultCards(Cards());
        changes.Should().Be(0);

        service.ApplyServerDefault();
        service.SetServerDefaultCards(null);

        changes.Should().Be(2);
        service.EffectiveCards.Should().BeNull();
    }
}
