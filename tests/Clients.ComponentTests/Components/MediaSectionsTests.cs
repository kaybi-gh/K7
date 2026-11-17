using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MediaSectionsTests
{
    private static RenderFragment Content(string text) => builder => builder.AddContent(0, text);

    [Test]
    public void Render_ShouldAddNoElementOfItsOwn()
    {
        using var ctx = new BunitContext();
        var theme = new ThemeService();
        theme.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);
        ctx.Services.AddSingleton(theme);

        var cut = ctx.Render<MediaSections>(p => p.Add(x => x.ChildContent, Content("content of cast")));

        cut.Markup.Trim().Should().Be("content of cast");
    }

    [Test]
    public void GridRows_ShouldLimitGrids_WhenShowMoreIsOn()
    {
        using var ctx = new BunitContext();
        var theme = new ThemeService();
        theme.Apply(new AppearancePreferencesDto { Default = { MediaPageLists = MediaListStyle.Grid, MediaPageGridMore = true } }, DeviceType.Desktop);
        ctx.Services.AddSingleton(theme);

        var cut = ctx.Render<MediaSections>(p => p.Add(x => x.ChildContent, Content("cast")));
        cut.Instance.GridRows.Should().Be(2);

        theme.Apply(new AppearancePreferencesDto { Default = { MediaPageLists = MediaListStyle.Grid } }, DeviceType.Desktop);
        cut.Instance.GridRows.Should().BeNull();
    }

    [Test]
    public void Grid_ShouldFollowTheListsSetting()
    {
        using var ctx = new BunitContext();
        var theme = new ThemeService();
        theme.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);
        ctx.Services.AddSingleton(theme);

        var cut = ctx.Render<MediaSections>(p => p.Add(x => x.ChildContent, Content("cast")));
        cut.Instance.Grid.Should().BeFalse();

        theme.Apply(new AppearancePreferencesDto { Default = { MediaPageLists = MediaListStyle.Grid } }, DeviceType.Desktop);
        cut.Instance.Grid.Should().BeTrue();
    }

    [Test]
    public void Poster_ShouldOnlyExist_WhenItIsAskedFor()
    {
        using var ctx = new BunitContext();
        var theme = new ThemeService();
        theme.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);
        ctx.Services.AddSingleton(theme);
        ctx.ComponentFactories.AddStub<K7Image>();

        var cut = ctx.Render<MediaHeroPoster>(p => p.Add(x => x.Src, "poster.jpg"));
        cut.FindAll(".media-hero-poster").Should().BeEmpty();

        theme.Apply(new AppearancePreferencesDto { Default = { MediaPagePoster = true } }, DeviceType.Desktop);
        cut.WaitForAssertion(() => cut.FindAll(".media-hero-poster").Should().ContainSingle());

        // A TV shows it too.
        theme.Apply(new AppearancePreferencesDto { Default = { MediaPagePoster = true } }, DeviceType.TV);
        cut.WaitForAssertion(() => cut.FindAll(".media-hero-poster").Should().ContainSingle());

        theme.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        cut.WaitForAssertion(() => cut.FindAll(".media-hero-poster").Should().BeEmpty());
    }
}
