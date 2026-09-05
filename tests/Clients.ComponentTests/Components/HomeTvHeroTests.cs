using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class HomeTvHeroTests
{
    [Test]
    public async Task ApplyFocusedItem_ShouldNotPreloadSkippedBackdrops_WhenFocusMovesRapidly()
    {
        using var ctx = CreateContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var first = Card("1", "https://img.test/a.jpg");
        var cut = ctx.Render<HomeTvHero>(p => p.Add(h => h.Model, first));

        await cut.WaitForAssertionAsync(() =>
            CountPreloads(ctx).Should().Be(1));

        await cut.InvokeAsync(() =>
        {
            cut.Instance.ApplyFocusedItem(Card("2", "https://img.test/b.jpg"));
            cut.Instance.ApplyFocusedItem(Card("3", "https://img.test/c.jpg"));
            cut.Instance.ApplyFocusedItem(Card("4", "https://img.test/d.jpg"));
        });

        await Task.Delay(80);
        CountPreloads(ctx).Should().Be(1);

        await cut.WaitForAssertionAsync(
            () => CountPreloads(ctx).Should().Be(2),
            timeout: TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ApplyFocusedItem_ShouldUpdateTitleImmediately()
    {
        using var ctx = CreateContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<HomeTvHero>(p => p.Add(h => h.Model, Card("1", "https://img.test/a.jpg")));
        cut.Find(".home-tv-hero__title").TextContent.Should().Be("One");

        await cut.InvokeAsync(() =>
            cut.Instance.ApplyFocusedItem(Card("2", "https://img.test/b.jpg", "Two")));

        cut.Find(".home-tv-hero__title").TextContent.Should().Be("Two");
    }

    [Test]
    public async Task ModelChange_ShouldSwapBackdrop_WhenParentPassesNewItem()
    {
        using var ctx = CreateContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<HomeTvHero>(p => p.Add(h => h.Model, Card("1", "https://img.test/a.jpg")));
        await cut.WaitForAssertionAsync(() =>
            CountPreloads(ctx).Should().Be(1));

        cut.Render(p => p.Add(h => h.Model, Card("2", "https://img.test/b.jpg", "Two")));
        cut.Find(".home-tv-hero__title").TextContent.Should().Be("Two");

        await cut.WaitForAssertionAsync(
            () => CountPreloads(ctx).Should().Be(2),
            timeout: TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ApplyFocusedItem_ShouldStillSwap_WhenParentRendersSameFocusedItem()
    {
        using var ctx = CreateContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var first = Card("1", "https://img.test/a.jpg");
        var second = Card("2", "https://img.test/b.jpg", "Two");
        var cut = ctx.Render<HomeTvHero>(p => p.Add(h => h.Model, first));
        await cut.WaitForAssertionAsync(() =>
            CountPreloads(ctx).Should().Be(1));

        await cut.InvokeAsync(() => cut.Instance.ApplyFocusedItem(second));
        cut.Render(p => p.Add(h => h.Model, second));

        await cut.WaitForAssertionAsync(
            () => CountPreloads(ctx).Should().Be(2),
            timeout: TimeSpan.FromSeconds(2));
    }

    private static int CountPreloads(BunitContext ctx) =>
        ctx.JSInterop.Invocations.Count(i => i.Identifier == "K7.preloadImage");

    private static MediaCardViewModel Card(string id, string backdropUrl, string? title = null) => new()
    {
        Id = id,
        Title = title ?? (id == "1" ? "One" : id),
        BackdropUrl = backdropUrl,
        Kind = MediaCardKind.Poster,
        MediaType = MediaType.Movie
    };

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);
        return ctx;
    }
}
