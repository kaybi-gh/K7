using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7VirtualGridTests
{
    [Test]
    public void Render_ShouldNotShowSkeletons_WhenProviderWidthIsUnknown()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7VirtualGrid<string>>(p => p
            .Add(c => c.ItemsProvider, _ => ValueTask.FromResult(new ItemsProviderResult<string>([], 0)))
            .Add<string>(c => c.ItemTemplate, item => item));

        cut.Find(".k7-virtual-grid").Should().NotBeNull();
        cut.FindAll(".media-card").Should().BeEmpty();
        cut.FindAll(".k7-skeleton").Should().BeEmpty();
        cut.FindAll(".k7-spinner").Should().BeEmpty();
        cut.Find(".k7-virtual-grid").GetAttribute("style").Should().Contain("--item-ratio: 2 / 3");
    }

    [Test]
    public void Render_ShouldUseBackdropRatio_WhenPlaceholderVariantIsBackdrop()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7VirtualGrid<string>>(p => p
            .Add(c => c.ItemsProvider, _ => ValueTask.FromResult(new ItemsProviderResult<string>([], 0)))
            .Add(c => c.PlaceholderVariant, MediaCardVariant.Backdrop)
            .Add<string>(c => c.ItemTemplate, item => item));

        cut.Find(".k7-virtual-grid").GetAttribute("style").Should().Contain("--item-ratio: 16 / 9");
    }

    [Test]
    public void Render_ShouldUseCoverRatio_WhenPlaceholderVariantIsCover()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7VirtualGrid<string>>(p => p
            .Add(c => c.ItemsProvider, _ => ValueTask.FromResult(new ItemsProviderResult<string>([], 0)))
            .Add(c => c.PlaceholderVariant, MediaCardVariant.Cover)
            .Add<string>(c => c.ItemTemplate, item => item));

        cut.Find(".k7-virtual-grid").GetAttribute("style").Should().Contain("--item-ratio: 1 / 1");
    }
}
