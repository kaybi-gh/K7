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
    public async Task OnViewportChanged_ShouldHideTableMode_OnMobile()
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
        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("grid-alpha").And.NotContain("browse-table-marker"));

        // Assert - only grid remains on mobile when list is unavailable
        cut.Markup.Should().NotContain("browse-table-marker");
        cut.Markup.Should().Contain("grid-beta");
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
