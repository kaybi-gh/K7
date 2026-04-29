using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7MenuTests
{
    [Test]
    public void Render_ShouldNotHaveFocusOutHandler()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        // Act
        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.Label, "Test"));

        // Assert
        var dropdown = cut.Find(".k7-menu-dropdown");
        dropdown.HasAttribute("onfocusout").Should().BeFalse();
    }

    [Test]
    public async Task Toggle_ShouldCallPushLayer_WhenOpened()
    {
        // Arrange
        using var ctx = new BunitContext();
        var spatialNav = Substitute.For<ISpatialNavService>();
        ctx.Services.AddSingleton(spatialNav);

        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.Label, "Test"));

        // Act
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        await spatialNav.Received(1).PushLayerAsync(
            Arg.Any<Microsoft.AspNetCore.Components.ElementReference>(),
            "popover",
            Arg.Any<SpatialNavLayerOptions>());
    }

    [Test]
    public async Task Toggle_ShouldCallPopLayer_WhenClosed()
    {
        // Arrange
        using var ctx = new BunitContext();
        var spatialNav = Substitute.For<ISpatialNavService>();
        ctx.Services.AddSingleton(spatialNav);

        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.Label, "Test"));

        // Act - open then close
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());
        await cut.InvokeAsync(() => button.Click());

        // Assert
        await spatialNav.Received(1).PopLayerAsync(
            Arg.Any<Microsoft.AspNetCore.Components.ElementReference>());
    }
}
