using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SliderTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Slider<double>>();

        // Assert
        var input = cut.Find("input[type='range']");
        input.HasAttribute("data-sn-activatable").Should().BeTrue();
    }
}
