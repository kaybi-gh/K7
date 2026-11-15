using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class HomeHeroTests
{
    private static MediaCardViewModel Card(string id) => new()
    {
        Id = id,
        Title = $"Title {id}",
        BackdropUrl = $"https://img.test/{id}.jpg",
        Overview = $"Overview {id}",
        ReleaseYear = 2020
    };

    private static IRenderedComponent<HomeHero> Render(BunitContext ctx, params MediaCardViewModel[] items) =>
        ctx.Render<HomeHero>(p => p
            .Add(h => h.Items, items)
            .Add(h => h.Label, "New movies")
            .Add(h => h.HrefFor, item => $"/movies/{item.Id}"));

    [Test]
    public void Render_ShouldShowNothing_WhenThereIsNoItem()
    {
        using var ctx = CreateContext();

        Render(ctx).Markup.Trim().Should().BeEmpty();
    }

    [Test]
    public void Render_ShouldFeatureTheFirstItemAndLinkToItsPage()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Card("1"), Card("2"));

        cut.Find(".home-hero__title").TextContent.Should().Be("Title 1");
        cut.Find(".home-hero__overview").TextContent.Should().Be("Overview 1");
        cut.Find(".home-hero__actions a").GetAttribute("href").Should().Be("/movies/1");
        cut.FindAll(".home-hero__dot").Should().HaveCount(2);
    }

    [Test]
    public void Render_ShouldNameTheBannerAfterItsRow_WithoutShowingTheName()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Card("1"));

        cut.Find(".home-hero").GetAttribute("aria-label").Should().Be("New movies");
        cut.Find(".home-hero__content").TextContent.Should().NotContain("New movies");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Render_ShouldFlagTheBleedStyle(bool bleed)
    {
        using var ctx = CreateContext();

        var cut = ctx.Render<HomeHero>(p => p
            .Add(h => h.Items, new[] { Card("1") })
            .Add(h => h.Bleed, bleed)
            .Add(h => h.HrefFor, item => $"/movies/{item.Id}"));

        cut.Find(".home-hero").ClassList.Contains("home-hero--bleed").Should().Be(bleed);
    }

    [Test]
    public void Render_ShouldShowTheLogoInsteadOfTheTitle_WhenTheMediaHasOne()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Card("1") with { LogoUrl = "https://img.test/logo.png" });

        var logo = cut.Find(".home-hero__title img.home-hero__logo");
        logo.GetAttribute("src").Should().Be("https://img.test/logo.png");
        logo.GetAttribute("alt").Should().Be("Title 1");
    }

    [Test]
    public void Render_ShouldGiveTheTitleBack_WhenTheLogoFailsToLoad()
    {
        using var ctx = CreateContext();
        var cut = Render(ctx, Card("1") with { LogoUrl = "https://img.test/logo.png" });

        cut.Find(".home-hero__logo").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        cut.FindAll(".home-hero__logo").Should().BeEmpty();
        cut.Find(".home-hero__title").TextContent.Should().Be("Title 1");
    }

    [Test]
    public void Focus_ShouldBeRelayedToTheButton_WhenArrowKeysLandOnTheBanner()
    {
        using var ctx = CreateContext();
        var module = ctx.JSInterop.SetupModule(HomeHero.ModulePath);
        var relay = module.SetupVoid("focusPrimary", _ => true).SetVoidResult();
        var cut = Render(ctx, Card("1"));
        var banner = cut.Find(".home-hero");

        // Reachable by arrow keys (focusable), not by Tab (tabindex -1): Tab goes to the button itself.
        banner.ClassList.Should().Contain("focusable");
        banner.GetAttribute("tabindex").Should().Be("-1");

        banner.TriggerEvent("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        cut.WaitForAssertion(() => relay.Invocations.Should().ContainSingle());
    }

    [Test]
    public void Render_ShouldHideTheDots_WhenThereIsOneItem()
    {
        using var ctx = CreateContext();

        Render(ctx, Card("1")).FindAll(".home-hero__dot").Should().BeEmpty();
    }

    [Test]
    public void Dot_ShouldSwitchTheFeaturedItem_WhenClicked()
    {
        using var ctx = CreateContext();
        var cut = Render(ctx, Card("1"), Card("2"));

        cut.FindAll(".home-hero__dot")[1].Click();

        cut.Find(".home-hero__title").TextContent.Should().Be("Title 2");
        cut.FindAll(".home-hero__dot")[1].GetAttribute("aria-current").Should().Be("true");
    }

    [Test]
    public void Rerender_ShouldKeepTheChosenItem_WhenTheSameItemsComeBack()
    {
        using var ctx = CreateContext();
        var cut = Render(ctx, Card("1"), Card("2"));
        cut.FindAll(".home-hero__dot")[1].Click();

        cut.Render(p => p.Add(h => h.Items, new[] { Card("1"), Card("2") }));

        cut.Find(".home-hero__title").TextContent.Should().Be("Title 2");
    }

    [Test]
    public void Rerender_ShouldGoBackToTheFirstItem_WhenTheItemsChange()
    {
        using var ctx = CreateContext();
        var cut = Render(ctx, Card("1"), Card("2"));
        cut.FindAll(".home-hero__dot")[1].Click();

        cut.Render(p => p.Add(h => h.Items, new[] { Card("3"), Card("4") }));

        cut.Find(".home-hero__title").TextContent.Should().Be("Title 3");
    }

    private static IRenderedComponent<HomeHero> RenderAuto(BunitContext ctx, bool canMove = true)
    {
        ctx.JSInterop.SetupModule(HomeHero.ModulePath).Setup<bool>("canAutoAdvance").SetResult(canMove);
        return ctx.Render<HomeHero>(p => p
            .Add(h => h.Items, new[] { Card("1"), Card("2") })
            .Add(h => h.AutoAdvanceSeconds, 1)
            .Add(h => h.HrefFor, item => $"/movies/{item.Id}"));
    }

    [Test]
    public void AutoAdvance_ShouldMoveToTheNextTitle_AfterTheDelay()
    {
        using var ctx = CreateContext();

        var cut = RenderAuto(ctx);

        cut.WaitForAssertion(
            () => cut.Find(".home-hero__title").TextContent.Should().Be("Title 2"),
            TimeSpan.FromSeconds(4));
    }

    [Test]
    public async Task AutoAdvance_ShouldStayStill_WhenPausedHoveredOrNotAllowedToMove()
    {
        using var ctx = CreateContext();
        var paused = RenderAuto(ctx);
        paused.Find(".home-hero__toggle").Click();
        paused.Find(".home-hero__toggle").GetAttribute("aria-pressed").Should().Be("true");

        using var hoverCtx = CreateContext();
        var hovered = RenderAuto(hoverCtx);
        hovered.Find(".home-hero").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Reduced motion or a hidden page.
        using var stillCtx = CreateContext();
        var still = RenderAuto(stillCtx, canMove: false);

        await Task.Delay(2500);

        paused.Find(".home-hero__title").TextContent.Should().Be("Title 1");
        hovered.Find(".home-hero__title").TextContent.Should().Be("Title 1");
        still.Find(".home-hero__title").TextContent.Should().Be("Title 1");
    }

    [Test]
    public void AutoAdvance_ShouldShowAWaitIndicatorOnTheActiveDot()
    {
        using var ctx = CreateContext();

        var cut = RenderAuto(ctx);

        cut.FindAll(".home-hero__dot--timed").Should().ContainSingle()
            .Which.ClassList.Should().Contain("home-hero__dot--active");
        cut.FindAll(".home-hero__dot-fill").Should().ContainSingle();
        cut.Find(".home-hero").GetAttribute("style").Should().Contain("--home-hero-delay: 1s");
    }

    [Test]
    public void AutoAdvance_ShouldHoldItsIndicator_WhenPausedOrHovered()
    {
        using var ctx = CreateContext();
        var cut = RenderAuto(ctx);
        cut.Find(".home-hero").ClassList.Should().NotContain("home-hero--holding");

        cut.Find(".home-hero__toggle").Click();

        cut.Find(".home-hero").ClassList.Should().Contain("home-hero--holding");
    }

    [Test]
    public void AutoAdvance_ShouldHoldForKeyboardFocus_AndLetGoWhenTheFocusLeavesOrThePointerTakesOver()
    {
        using var ctx = CreateContext();
        var cut = RenderAuto(ctx);
        var hero = cut.Find(".home-hero");

        hero.TriggerEvent("onkeyup", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Tab" });
        cut.Find(".home-hero").ClassList.Should().Contain("home-hero--holding");

        cut.Find(".home-hero").TriggerEvent("onfocusout", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        cut.Find(".home-hero").ClassList.Should().NotContain("home-hero--holding");

        cut.Find(".home-hero").TriggerEvent("onkeyup", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Tab" });
        cut.Find(".home-hero").TriggerEvent("onpointerdown", new Microsoft.AspNetCore.Components.Web.PointerEventArgs());
        cut.Find(".home-hero").ClassList.Should().NotContain("home-hero--holding");
    }

    [Test]
    public void AutoAdvance_ShouldLetGo_WhenThePointerLeavesTheBanner()
    {
        using var ctx = CreateContext();
        var cut = RenderAuto(ctx);

        cut.Find(".home-hero").TriggerEvent("onmouseenter", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find(".home-hero").ClassList.Should().Contain("home-hero--holding");

        cut.Find(".home-hero").TriggerEvent("onmouseleave", new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find(".home-hero").ClassList.Should().NotContain("home-hero--holding");
    }

    [Test]
    public void Dot_ShouldKeepThePreviousArtworkUnderTheNewOne_WhenTheTitleChanges()
    {
        using var ctx = CreateContext();
        var cut = Render(ctx, Card("1"), Card("2"));
        cut.FindAll(".home-hero__backdrop--previous").Should().BeEmpty();

        cut.FindAll(".home-hero__dot")[1].Click();

        cut.Find(".home-hero__backdrop--previous").GetAttribute("src").Should().Be("https://img.test/1.jpg");
        cut.Find(".home-hero__backdrop--current").GetAttribute("src").Should().Be("https://img.test/2.jpg");
    }

    [Test]
    public async Task Render_ShouldStayStillWithoutPauseButton_WhenAutoAdvanceIsOff()
    {
        using var ctx = CreateContext();

        var cut = Render(ctx, Card("1"), Card("2"));
        await Task.Delay(1500);

        cut.FindAll(".home-hero__toggle").Should().BeEmpty();
        cut.FindAll(".home-hero__dot--timed").Should().BeEmpty();
        cut.Find(".home-hero__title").TextContent.Should().Be("Title 1");
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Localizer<IStringLocalizer<SharedResource>>());
        ctx.Services.AddSingleton(Localizer<IStringLocalizer<HomeHero>>());
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
