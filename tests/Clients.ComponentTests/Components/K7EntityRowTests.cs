using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7EntityRowTests
{
    [Test]
    public void Render_ShouldLinkToTheSheet_WhenNoClickHandlerIsSet()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7EntityRow>(p => p
            .Add(c => c.Title, "alice")
            .Add(c => c.Subtitle, "alice@example.com")
            .Add(c => c.Href, "/admin/users/1")
            .Add(c => c.Active, true));

        // Assert
        var row = cut.Find("a.k7-entity-row");
        row.GetAttribute("href").Should().Be("/admin/users/1");
        row.GetAttribute("aria-current").Should().Be("page");
        row.ClassList.Should().Contain("k7-entity-row--active");
        row.TextContent.Should().Contain("alice@example.com");
    }

    [Test]
    public void Render_ShouldBeAButtonWithASelectionMark_WhenClickHandlerIsSet()
    {
        // Arrange
        using var ctx = new BunitContext();
        var clicks = 0;

        // Act
        var cut = ctx.Render<K7EntityRow>(p => p
            .Add(c => c.Title, "alice")
            .Add(c => c.Checked, true)
            .Add(c => c.OnClick, EventCallback.Factory.Create(this, () => clicks++)));
        cut.Find("button.k7-entity-row").Click();

        // Assert
        clicks.Should().Be(1);
        cut.Find("button.k7-entity-row").ClassList.Should().Contain("k7-entity-row--checked");
        cut.FindAll(".k7-entity-row__check").Should().ContainSingle();
        cut.FindAll("a").Should().BeEmpty();
    }

    [Test]
    public void SheetAction_ShouldShowItsBadges()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7SheetAction>(p => p
            .Add(c => c.Label, "Content restrictions")
            .Add(c => c.Badges, new[] { "Profile applied", "Age limit" })
            .Add(c => c.Danger, true));

        // Assert
        var action = cut.Find("button.k7-sheet-action");
        action.ClassList.Should().Contain("k7-sheet-action--danger");
        action.TextContent.Should().Contain("Content restrictions");
        cut.FindAll(".k7-sheet-action__badges .k7-chip").Should().HaveCount(2);
    }
}
