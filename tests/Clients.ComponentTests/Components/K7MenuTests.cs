using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7MenuTests
{
    [Test]
    public void Render_ShouldNotHaveFocusOutHandler()
    {
        using var ctx = CreateContext();

        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.ActivatorContent, "Test"));

        var dropdown = cut.Find(".k7-menu-dropdown");
        dropdown.HasAttribute("onfocusout").Should().BeFalse();
    }

    [Test]
    public async Task Toggle_ShouldCallPushLayer_WhenOpened()
    {
        using var ctx = CreateContext();
        var spatialNav = ctx.Services.GetRequiredService<ISpatialNavService>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.ActivatorContent, "Test"));

        var button = cut.Find(".k7-menu-activator-inner");
        await cut.InvokeAsync(async () =>
        {
            button.Click();
            await cut.WaitForStateAsync(() => cut.Find(".k7-menu--open") is not null);
        });

        await spatialNav.Received(1).PushLayerAsync(
            Arg.Any<Microsoft.AspNetCore.Components.ElementReference>(),
            "popover",
            Arg.Any<SpatialNavLayerOptions>());
    }

    [Test]
    public async Task Render_ShouldShowHeader_WhenTitleIsSet()
    {
        using var ctx = CreateContext();

        var cut = ctx.Render<K7Menu>(p => p
            .Add(m => m.ActivatorContent, "Open")
            .Add(m => m.Title, "Actions on Movie")
            .Add(m => m.Open, true));

        cut.Find(".k7-menu-header").TextContent.Trim().Should().Be("Actions on Movie");
    }

    [Test]
    public async Task Toggle_ShouldCallPopLayer_WhenClosed()
    {
        using var ctx = CreateContext();
        var spatialNav = ctx.Services.GetRequiredService<ISpatialNavService>();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7Menu>(p => p.Add(m => m.ActivatorContent, "Test"));

        var button = cut.Find(".k7-menu-activator-inner");
        await cut.InvokeAsync(() => button.Click());
        await cut.InvokeAsync(() => button.Click());

        await spatialNav.Received(1).PopLayerAsync(
            Arg.Any<Microsoft.AspNetCore.Components.ElementReference>());
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        return ctx;
    }
}
