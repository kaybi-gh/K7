using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SelectTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        // Act
        var cut = ctx.Render<K7Select<string>>();

        // Assert
        var select = cut.Find("button.k7-select");
        select.ClassList.Should().Contain("focusable");
    }
}
