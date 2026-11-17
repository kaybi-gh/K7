using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class PersonCardTests
{
    private static IRenderedComponent<PersonCard> Render(BunitContext ctx, CardHoverStyle? hover, string? href = "/persons/1")
    {
        ctx.ComponentFactories.AddStub<K7Avatar>();
        return ctx.Render<PersonCard>(p =>
        {
            p.Add(x => x.Href, href).Add(x => x.Title, "Someone");
            if (hover is not null)
                p.AddCascadingValue(new MediaCardStyleContext(new MediaCardPreferencesDto { Default = new MediaCardStyleDto { Hover = hover.Value } }));
        });
    }

    [TestCase(CardHoverStyle.Zoom, "personrole-item--hover-zoom")]
    [TestCase(CardHoverStyle.Lift, "personrole-item--hover-lift")]
    public void Render_ShouldTakeTheHoverEffectOfTheCardStyle(CardHoverStyle hover, string expected)
    {
        using var ctx = new BunitContext();

        Render(ctx, hover).Find(".personrole-item").ClassList.Should().Contain(expected);
    }

    [Test]
    public void Render_ShouldKeepTheOutline_ByDefault()
    {
        using var ctx = new BunitContext();

        Render(ctx, null).Find(".personrole-item").ClassName.Should().NotContain("--hover-");
        Render(ctx, CardHoverStyle.Outline).Find(".personrole-item").ClassName.Should().NotContain("--hover-");
    }

    [Test]
    public void Render_ShouldHaveNoHoverEffect_WhenTheCardIsNotALink()
    {
        using var ctx = new BunitContext();

        Render(ctx, CardHoverStyle.Zoom, href: null).Find(".personrole-item").ClassName.Should().NotContain("--hover-");
    }
}
