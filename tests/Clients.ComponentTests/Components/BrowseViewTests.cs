using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class BrowseViewTests
{
    [Test]
    public async Task OnViewportChanged_ShouldPreferGridOverTable_OnMobile()
    {
        // Arrange
        using var ctx = CreateContext();
        var items = new List<string> { "alpha", "beta" };

        var cut = ctx.Render<BrowseView<string>>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.DefaultMode, BrowseViewMode.Table)
            .Add(x => x.GridItemAspectRatio, (float?)null)
            .Add(x => x.GridTemplate, item => (RenderFragment)(builder =>
                builder.AddContent(0, $"grid-{item}")))
            .Add(x => x.TableContent, builder =>
                builder.AddMarkupContent(0, "<div class=\"browse-table-marker\">table</div>")));

        // Act
        await cut.InvokeAsync(() => cut.Instance.OnViewportChanged(true));

        // Assert - grid becomes the active surface; table stays mounted but inert
        // so Virtualize is not destroyed on the next desktop switch.
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("grid-alpha");
            cut.Markup.Should().Contain("browse-view-surface is-active");
            var active = cut.Find(".browse-view-surface.is-active");
            active.TextContent.Should().Contain("grid-alpha");
            cut.Find(".browse-view-surface.is-inactive .browse-table-marker").Should().NotBeNull();
            cut.Find(".browse-view-surface.is-inactive").HasAttribute("inert").Should().BeTrue();
        });
    }

    [Test]
    public void Render_ShouldKeepGridAndTableMounted_OnDesktop()
    {
        using var ctx = CreateContext();
        var items = new List<string> { "alpha" };

        var cut = ctx.Render<BrowseView<string>>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.DefaultMode, BrowseViewMode.Grid)
            .Add(x => x.DisableViewModePersistence, true)
            .Add(x => x.GridItemAspectRatio, (float?)null)
            .Add(x => x.GridTemplate, item => (RenderFragment)(builder =>
                builder.AddContent(0, $"grid-{item}")))
            .Add(x => x.TableContent, builder =>
                builder.AddMarkupContent(0, "<div class=\"browse-table-marker\">table</div>")));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".browse-view-surface").Count.Should().Be(2);
            cut.Find(".browse-view-surface.is-active").TextContent.Should().Contain("grid-alpha");
            cut.Find(".browse-view-surface.is-inactive .browse-table-marker").Should().NotBeNull();
        });
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = ctx.JSInterop.SetupModule("./_content/K7.Clients.Shared.UI/js/browseView.js");
        module.Setup<bool>("observeViewport").SetResult(true);
        module.SetupVoid("disposeViewport");
        module.SetupVoid("disposeSentinel");
        module.SetupVoid("saveSettings");

        return ctx;
    }
}
