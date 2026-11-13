using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7OptionListTests
{
    private static readonly IReadOnlyList<ButtonGroupOption<string>> Options =
    [
        new("a", "First"),
        new("b", "Second"),
        new("c", "Third")
    ];

    [Test]
    public void Render_ShouldMarkOnlyTheCurrentValueAsChecked()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7OptionList<string>>(p => p
            .Add(c => c.Options, Options)
            .Add(c => c.Value, "b"));

        // Assert
        var items = cut.FindAll("[role=radio]");
        items.Should().HaveCount(3);
        items.Select(i => i.GetAttribute("aria-checked")).Should().Equal("false", "true", "false");
    }

    [Test]
    public void Click_ShouldRaiseValueChanged_WhenOptionIsNotSelected()
    {
        // Arrange
        using var ctx = new BunitContext();
        string? picked = null;
        var cut = ctx.Render<K7OptionList<string>>(p => p
            .Add(c => c.Options, Options)
            .Add(c => c.Value, "a")
            .Add(c => c.ValueChanged, value => picked = value));

        // Act
        cut.FindAll("[role=radio]")[2].Click();

        // Assert
        picked.Should().Be("c");
    }

    [Test]
    public void Click_ShouldNotRaiseValueChanged_WhenOptionIsAlreadySelected()
    {
        // Arrange
        using var ctx = new BunitContext();
        var raised = false;
        var cut = ctx.Render<K7OptionList<string>>(p => p
            .Add(c => c.Options, Options)
            .Add(c => c.Value, "a")
            .Add(c => c.ValueChanged, _ => raised = true));

        // Act
        cut.FindAll("[role=radio]")[0].Click();

        // Assert
        raised.Should().BeFalse();
    }
}
