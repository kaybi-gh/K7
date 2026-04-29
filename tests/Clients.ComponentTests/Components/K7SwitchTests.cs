using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SwitchTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Switch>();

        // Assert
        var label = cut.Find("label");
        label.HasAttribute("data-sn-activatable").Should().BeTrue();
    }

    [Test]
    public void Render_ShouldHaveTabIndexZero()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Switch>();

        // Assert
        var label = cut.Find("label");
        label.GetAttribute("tabindex").Should().Be("0");
    }

    [Test]
    public void Render_InnerCheckbox_ShouldHaveTabIndexMinusOne()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Switch>();

        // Assert
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.GetAttribute("tabindex").Should().Be("-1");
    }
}
