using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class SkinPreviewTests
{
    private static IRenderedComponent<SkinPreview> Render(BunitContext ctx, SkinPreviewKind kind, AppearanceSettingsDto settings) =>
        ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, kind)
            .Add(x => x.Settings, settings)
            .Add(x => x.SampleTitle, "Sample"));

    [Test]
    public void MediaPage_ShouldDrawThePoster_OnlyWhenAskedFor()
    {
        using var ctx = new BunitContext();

        var plain = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto());
        plain.Find(".skin-preview").ClassName.Should().Be("skin-preview skin-preview--page");
        plain.FindAll(".sp-poster").Should().BeEmpty();
        plain.FindAll(".sp-row").Should().HaveCount(2);

        Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPagePoster = true })
            .FindAll(".sp-poster").Should().ContainSingle();
    }

    [TestCase(MediaHeroStyle.Cut, MediaHeroSize.Half, "skin-preview--hero-cut", "skin-preview--size-half")]
    [TestCase(MediaHeroStyle.Banner, MediaHeroSize.Third, "skin-preview--hero-banner", "skin-preview--size-third")]
    public void MediaPage_ShouldDrawTheStyleAndTheSizeOfTheBackdrop(MediaHeroStyle backdrop, MediaHeroSize size, string style, string sized)
    {
        using var ctx = new BunitContext();

        var full = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPageHero = backdrop });
        full.Find(".skin-preview").ClassList.Should().Contain(style).And.NotContain(sized);

        var smaller = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPageHero = backdrop, MediaPageHeroSize = size });
        smaller.Find(".skin-preview").ClassList.Should().Contain(style).And.Contain(sized);
    }

    [Test]
    public void MediaPage_ShouldDrawGrids_WhenListsAreGrids()
    {
        using var ctx = new BunitContext();

        var carousel = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto());
        carousel.FindAll(".sp-row--grid").Should().BeEmpty();

        var grid = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPageLists = MediaListStyle.Grid });
        grid.FindAll(".sp-row--grid").Should().HaveCount(2);
        grid.FindAll(".sp-card").Count.Should().BeGreaterThan(carousel.FindAll(".sp-card").Count);
    }

    [Test]
    public void Phone_ShouldGetAnUprightSketch_WithTheSameHeroAndNoPoster()
    {
        using var ctx = new BunitContext();
        var settings = new AppearanceSettingsDto
        {
            MediaPageHero = MediaHeroStyle.Banner,
            MediaPageHeroSize = MediaHeroSize.Half,
            MediaPagePoster = true,
            MediaPageLists = MediaListStyle.Grid
        };

        var desktop = ctx.Render<SkinPreview>(p => p.Add(x => x.Kind, SkinPreviewKind.MediaPage).Add(x => x.Settings, settings));
        desktop.Find(".skin-preview").ClassList.Should().NotContain("skin-preview--phone");
        desktop.FindAll(".sp-poster").Should().ContainSingle();

        var phone = ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, SkinPreviewKind.MediaPage)
            .Add(x => x.Settings, settings)
            .Add(x => x.Device, DeviceType.Phone));
        phone.Find(".skin-preview").ClassName.Should().Be(
            "skin-preview skin-preview--phone skin-preview--page skin-preview--hero-banner skin-preview--size-half");
        phone.FindAll(".sp-poster").Should().BeEmpty();
        phone.FindAll(".sp-row--grid").Should().HaveCount(2);
        phone.FindAll(".sp-card").Count.Should().BeLessThan(desktop.FindAll(".sp-card").Count);
    }

    [Test]
    public void Phone_ShouldKeepTheVideoPlayerOnItsSide()
    {
        using var ctx = new BunitContext();

        ctx.Render<SkinPreview>(p => p
                .Add(x => x.Kind, SkinPreviewKind.AudioPlayer)
                .Add(x => x.Settings, new AppearanceSettingsDto())
                .Add(x => x.Device, DeviceType.Phone))
            .Find(".skin-preview").ClassList.Should().Contain("skin-preview--phone");

        ctx.Render<SkinPreview>(p => p
                .Add(x => x.Kind, SkinPreviewKind.VideoPlayer)
                .Add(x => x.Settings, new AppearanceSettingsDto())
                .Add(x => x.Device, DeviceType.Phone))
            .Find(".skin-preview").ClassList.Should().NotContain("skin-preview--phone");
    }

    [Test]
    public void Tv_ShouldDrawTheSameSettings()
    {
        using var ctx = new BunitContext();

        var tv = ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, SkinPreviewKind.MediaPage)
            .Add(x => x.Settings, new AppearanceSettingsDto
            {
                MediaPageHero = MediaHeroStyle.Cut,
                MediaPageHeroSize = MediaHeroSize.Half,
                MediaPagePoster = true,
                MediaPageLists = MediaListStyle.Grid
            })
            .Add(x => x.Device, DeviceType.TV));

        tv.Find(".skin-preview").ClassName.Should().Be("skin-preview skin-preview--page skin-preview--hero-cut skin-preview--size-half");
        tv.FindAll(".sp-poster").Should().ContainSingle();
        tv.FindAll(".sp-row--grid").Should().HaveCount(2);
    }

    [Test]
    public void MediaPage_ShouldDrawTheListsOfTheKindOfPage()
    {
        using var ctx = new BunitContext();
        IRenderedComponent<SkinPreview> Draw(MediaPageKind? kind) => ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, SkinPreviewKind.MediaPage)
            .Add(x => x.Settings, new AppearanceSettingsDto { MediaPagePoster = true })
            .Add(x => x.PageKind, kind));

        // A movie: the cast, then similar titles.
        var movie = Draw(MediaPageKind.Movie);
        movie.FindAll(".sp-row")[0].QuerySelectorAll(".sp-card--round").Should().NotBeEmpty();
        movie.FindAll(".sp-poster").Should().ContainSingle();

        // A series opens on its seasons, an episode on the other episodes.
        Draw(MediaPageKind.Series).FindAll(".sp-row")[0].QuerySelectorAll(".sp-card--round").Should().BeEmpty();
        Draw(MediaPageKind.Episode).FindAll(".sp-row")[0].QuerySelectorAll(".sp-card--wide").Should().NotBeEmpty();

        // A person: a portrait, no poster.
        var person = Draw(MediaPageKind.Person);
        person.FindAll(".sp-avatar").Should().ContainSingle();
        person.FindAll(".sp-poster").Should().BeEmpty();
    }

    [Test]
    public void Season_ShouldDrawAList_OrTheImmersivePage()
    {
        using var ctx = new BunitContext();
        IRenderedComponent<SkinPreview> Draw(AppearanceSettingsDto settings, DeviceType device) => ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, SkinPreviewKind.MediaPage)
            .Add(x => x.Settings, settings)
            .Add(x => x.Device, device)
            .Add(x => x.PageKind, MediaPageKind.Season));

        var list = Draw(new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Banner, MediaPageLists = MediaListStyle.Grid }, DeviceType.Desktop);
        list.Find(".skin-preview").ClassName.Should().Be("skin-preview skin-preview--page skin-preview--season-list");
        list.FindAll(".sp-episode").Should().HaveCount(4);
        list.FindAll(".sp-backdrop").Should().BeEmpty();

        var immersive = Draw(new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.Carousel, MediaPageLists = MediaListStyle.Grid }, DeviceType.TV);
        immersive.Find(".skin-preview").ClassName.Should().Be("skin-preview skin-preview--page skin-preview--season");
        immersive.FindAll(".sp-episode").Should().BeEmpty();
        immersive.FindAll(".sp-row").Should().ContainSingle();
        // Its episodes always are a carousel.
        immersive.FindAll(".sp-row--grid").Should().BeEmpty();

        Draw(new AppearanceSettingsDto(), DeviceType.TV).FindAll(".sp-episode").Should().BeEmpty();

        // Only a TV shows the carousel page: a computer asked for it draws the list.
        Draw(new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.Carousel }, DeviceType.Desktop).FindAll(".sp-episode").Should().HaveCount(4);

        // A grid of episode cards under the title, on a phone too.
        var grid = Draw(new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.Grid }, DeviceType.Phone);
        grid.Find(".skin-preview").ClassList.Should().Contain("skin-preview--season-list");
        grid.FindAll(".sp-episode").Should().BeEmpty();
        grid.FindAll(".sp-row--grid .sp-card--wide").Should().NotBeEmpty();

        // A TV keeps the carousel page whatever the setting.
        Draw(new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.List }, DeviceType.TV).FindAll(".sp-episode").Should().BeEmpty();
        Draw(new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.Grid }, DeviceType.TV).FindAll(".sp-row--grid").Should().BeEmpty();
    }

    [Test]
    public void MediaPage_ShouldDrawTheSettingsOfTheKindOfPage()
    {
        using var ctx = new BunitContext();
        var settings = new AppearanceSettingsDto
        {
            MediaPageOverrides = { [MediaPageKind.Series] = new MediaPageSkinDto { Hero = MediaHeroStyle.Cut, Poster = true } }
        };

        var shared = Render(ctx, SkinPreviewKind.MediaPage, settings);
        shared.Find(".skin-preview").ClassName.Should().Be("skin-preview skin-preview--page");
        shared.FindAll(".sp-poster").Should().BeEmpty();

        var series = ctx.Render<SkinPreview>(p => p
            .Add(x => x.Kind, SkinPreviewKind.MediaPage)
            .Add(x => x.Settings, settings)
            .Add(x => x.PageKind, MediaPageKind.Series));
        series.Find(".skin-preview").ClassList.Should().Contain("skin-preview--hero-cut");
        series.FindAll(".sp-poster").Should().ContainSingle();
    }

    [Test]
    public void MediaPage_ShouldDrawShowMore_UnderLimitedGrids()
    {
        using var ctx = new BunitContext();

        var full = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPageLists = MediaListStyle.Grid });
        full.FindAll(".sp-more").Should().BeEmpty();

        var limited = Render(ctx, SkinPreviewKind.MediaPage, new AppearanceSettingsDto { MediaPageLists = MediaListStyle.Grid, MediaPageGridMore = true });
        limited.FindAll(".sp-more").Should().HaveCount(2);
        limited.FindAll(".sp-card").Count.Should().BeLessThan(full.FindAll(".sp-card").Count);
    }

    [TestCase(AudioSeekStyle.Waveform, ".sp-wave")]
    [TestCase(AudioSeekStyle.Bar, ".sp-seek")]
    [TestCase(AudioSeekStyle.Ring, ".sp-ring")]
    [TestCase(AudioSeekStyle.PlainRing, ".sp-ring")]
    public void AudioPlayer_ShouldDrawThePickedProgress(AudioSeekStyle seek, string expected)
    {
        using var ctx = new BunitContext();

        var cut = Render(ctx, SkinPreviewKind.AudioPlayer, new AppearanceSettingsDto { AudioSeek = seek });

        cut.FindAll(".sp-wave, .sp-seek, .sp-ring").Should().ContainSingle();
        cut.FindAll(expected).Should().ContainSingle();
    }

    [Test]
    public void AudioPlayer_ShouldCarryTheSameSkinsAsThePlayer()
    {
        using var ctx = new BunitContext();

        var cut = Render(ctx, SkinPreviewKind.AudioPlayer, new AppearanceSettingsDto
        {
            AudioBackground = AudioPlayerBackground.Gradient,
            AudioLayout = AudioPlayerLayout.Centered,
            AudioSeek = AudioSeekStyle.Ring,
            AudioAccent = PlayerAccent.Theme
        });

        cut.Find(".skin-preview").ClassList.Should().Contain(
            ["sp-audio--bg-gradient", "sp-audio--centered", "sp-audio--round", "sp-audio--ring", "sp-audio--accent-theme"]);
    }

    [Test]
    public void VideoPlayer_ShouldDrawEachOption_OnlyWhenItIsOn()
    {
        using var ctx = new BunitContext();

        var plain = Render(ctx, SkinPreviewKind.VideoPlayer, new AppearanceSettingsDto());
        plain.FindAll(".sp-video-center, .sp-poster, .sp-logo").Should().BeEmpty();
        plain.Find(".sp-video-top .sp-title").TextContent.Should().Be("Sample");

        var skinned = Render(ctx, SkinPreviewKind.VideoPlayer, new AppearanceSettingsDto
        {
            VideoControls = VideoControlsLayout.Stacked,
            VideoCenterControls = true,
            VideoPoster = true,
            VideoLogo = true,
            VideoAccent = PlayerAccent.Theme
        });
        skinned.FindAll(".sp-video-center").Should().ContainSingle();
        skinned.FindAll(".sp-poster").Should().ContainSingle();
        skinned.Find(".sp-logo").TextContent.Should().Be("Sample");
        skinned.Find(".skin-preview").ClassList.Should().Contain(["sp-video--stacked", "sp-video--accent-theme"]);
    }
}
