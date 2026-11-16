using K7.Clients.Shared.UI.Components;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class CustomNavPreviewTests
{
    private static IRenderedComponent<CustomNavPreview> Render(
        BunitContext ctx,
        bool enabled,
        CustomNavPlacement placement,
        bool icons = false,
        string? rowTitle = null,
        params string[] items) =>
        ctx.Render<CustomNavPreview>(p => p
            .Add(x => x.Enabled, enabled)
            .Add(x => x.Placement, placement)
            .Add(x => x.ShowIcons, icons)
            .Add(x => x.RowTitle, rowTitle)
            .Add(x => x.Items, items));

    [Test]
    public void Render_ShouldDrawThePlainPage_WhenTheNavigationIsOff()
    {
        using var ctx = new BunitContext();

        var cut = Render(ctx, enabled: false, CustomNavPlacement.Bar, items: ["Movies"]);

        cut.FindAll(".np-bar").Should().BeEmpty();
        cut.FindAll(".np-tile").Should().BeEmpty();
        cut.FindAll(".np-row").Should().HaveCount(2);
    }

    [Test]
    public void Render_ShouldDrawTheShortcutsInABar_WithIconsWhenAskedFor()
    {
        using var ctx = new BunitContext();

        var plain = Render(ctx, enabled: true, CustomNavPlacement.Bar, items: ["Movies", "Series"]);
        plain.FindAll(".np-chip").Select(c => c.TextContent.Trim()).Should().Equal("Movies", "Series");
        plain.FindAll(".np-chip-icon").Should().BeEmpty();
        plain.FindAll(".np-tile").Should().BeEmpty();

        Render(ctx, enabled: true, CustomNavPlacement.Bar, icons: true, items: ["Movies", "Series"])
            .FindAll(".np-chip-icon").Should().HaveCount(2);
    }

    [Test]
    public void Render_ShouldDrawTheShortcutsAsARow_UnderItsTitle()
    {
        using var ctx = new BunitContext();

        var cut = Render(ctx, enabled: true, CustomNavPlacement.FeedRow, rowTitle: "My shortcuts", items: ["Movies", "Series"]);

        cut.FindAll(".np-bar").Should().BeEmpty();
        cut.Find(".np-row-name").TextContent.Should().Be("My shortcuts");
        cut.FindAll(".np-tile").Select(t => t.TextContent.Trim()).Should().Equal("Movies", "Series");
        cut.FindAll(".np-row").Should().HaveCount(3);
    }

    [Test]
    public void Render_ShouldDrawTheRowWithoutATitle_WhenNoneIsSet()
    {
        using var ctx = new BunitContext();

        var cut = Render(ctx, enabled: true, CustomNavPlacement.FeedRow, items: ["Movies"]);

        cut.FindAll(".np-row-name").Should().BeEmpty();
        cut.FindAll(".np-tile").Should().ContainSingle();
        // Only the two rows of the page keep the line standing for their title.
        cut.FindAll(".np-row-title").Should().HaveCount(2);
    }

    [Test]
    public void Render_ShouldDrawBlankShortcuts_WhenThereIsNone_AndStopAtWhatFits()
    {
        using var ctx = new BunitContext();

        Render(ctx, enabled: true, CustomNavPlacement.Bar).FindAll(".np-chip").Should().HaveCount(3);

        var many = Enumerable.Range(1, CustomNavPreview.MaxShortcuts + 4).Select(i => $"Item {i}").ToArray();
        Render(ctx, enabled: true, CustomNavPlacement.FeedRow, items: many)
            .FindAll(".np-tile").Should().HaveCount(CustomNavPreview.MaxShortcuts);
    }
}
