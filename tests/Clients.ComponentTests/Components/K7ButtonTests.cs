using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7ButtonTests
{
    [Test]
    public void Render_ShouldUseButtonElement_WhenHrefMissing()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<K7Button>(parameters => parameters
            .AddChildContent("Play"));

        cut.Find("button").TextContent.Should().Contain("Play");
        cut.FindAll("a").Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldUseAnchorElement_WhenHrefProvided()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<K7Button>(parameters => parameters
            .Add(p => p.Href, "/browse")
            .AddChildContent("Browse"));

        var anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Be("/browse");
        cut.FindAll("button").Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldApplyDisabledState()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<K7Button>(parameters => parameters
            .Add(p => p.Disabled, true)
            .AddChildContent("Disabled"));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button").ClassList.Should().Contain("k7-btn--disabled");
    }
}
