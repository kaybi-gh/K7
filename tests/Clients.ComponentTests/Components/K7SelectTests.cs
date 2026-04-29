using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SelectTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Select<string>>();

        // Assert
        var select = cut.Find("select");
        select.HasAttribute("data-sn-activatable").Should().BeTrue();
    }
}
