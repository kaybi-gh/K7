using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class HomeLayoutPreviewTests
{
    private static HomeRowEditModel Row(string title, int order = 0) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        OrderBy = MediaOrderingOption.CreatedDesc,
        PageSize = 20,
        IsVisible = true,
        Order = order
    };

    [Test]
    public void Render_ShouldFetchEachVisibleRowOnce()
    {
        using var ctx = CreateContext(out var media);
        var rows = new List<HomeRowEditModel> { Row("Movies"), Row("Hidden", 1) };
        rows[1].IsVisible = false;
        rows[1].PageSize = 50;

        var cut = ctx.Render<HomeLayoutPreview>(p => p.Add(c => c.Rows, rows));

        cut.WaitForAssertion(() => cut.FindAll(".home-preview__row").Should().ContainSingle());
        media.Received(1).GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Rerender_ShouldNotFetchAgain_WhenOnlyOrderVisibilityOrCardFormatChanged()
    {
        using var ctx = CreateContext(out var media);
        var rows = new List<HomeRowEditModel> { Row("Movies"), Row("Series", 1) };
        rows[1].PageSize = 50;
        var cut = ctx.Render<HomeLayoutPreview>(p => p.Add(c => c.Rows, rows));
        cut.WaitForAssertion(() =>
            media.Received(2).GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>()));
        media.ClearReceivedCalls();

        (rows[0].Order, rows[1].Order) = (1, 0);
        rows[0].DisplayType = HomeRowDisplayType.Landscape;
        cut.Render(p => p.Add(c => c.Rows, rows));

        media.DidNotReceive().GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Rerender_ShouldFetchARowAgain_WhenWhatItListsChanged()
    {
        using var ctx = CreateContext(out var media);
        var rows = new List<HomeRowEditModel> { Row("Movies") };
        var cut = ctx.Render<HomeLayoutPreview>(p => p.Add(c => c.Rows, rows));
        cut.WaitForAssertion(() =>
            media.Received(1).GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>()));
        media.ClearReceivedCalls();

        rows[0].PageSize = 10;
        cut.Render(p => p.Add(c => c.Rows, rows));

        cut.WaitForAssertion(() => media.Received(1).GetHomeFeedAsync(
            Arg.Is<GetHomeFeedQuery>(q => q.PageSize == 10), Arg.Any<CancellationToken>()));
    }

    [Test]
    public void Render_ShouldBeAPictureToLookAt_NotToUse()
    {
        using var ctx = CreateContext(out _);

        var cut = ctx.Render<HomeLayoutPreview>(p => p.Add(c => c.Rows, new List<HomeRowEditModel> { Row("Movies") }));

        // inert: nothing in the preview takes the focus or a click.
        cut.Find(".home-preview__frame").HasAttribute("inert").Should().BeTrue();
    }

    private static BunitContext CreateContext(out IMediaService media)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        media = Substitute.For<IMediaService>();
        media.GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PaginatedListDto<HomeFeedItemDto>?>(null));
        ctx.Services.AddSingleton(media);
        ctx.Services.AddSingleton(Substitute.For<IK7ServerService>());
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.Services.AddSingleton(Localizer<IStringLocalizer<HomeLayoutPreview>>());
        ctx.Services.AddSingleton(Localizer<IStringLocalizer<Home>>());
        return ctx;
    }

    private static T Localizer<T>()
        where T : class, IStringLocalizer
    {
        var localizer = Substitute.For<T>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        return localizer;
    }
}
