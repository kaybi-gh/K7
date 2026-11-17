using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SkinClassesTests
{
    [Test]
    public void Everything_ShouldAddNoClass_ByDefault()
    {
        var settings = new AppearanceSettingsDto();

        SkinClasses.Shell(settings, DeviceType.Desktop).Should().BeNull();
        SkinClasses.AudioPlayer(settings).Should().BeEmpty();
        SkinClasses.VideoControls(settings).Should().BeEmpty();
        SkinClasses.PagePoster(settings, DeviceType.Desktop).Should().BeFalse();
        SkinClasses.PageGrid(settings, DeviceType.Desktop).Should().BeFalse();
    }

    [TestCase(MediaHeroStyle.Cut, "media-pages--hero-cut")]
    [TestCase(MediaHeroStyle.Banner, "media-pages--hero-banner")]
    public void Shell_ShouldCarryTheBackdropOfMediaPages(MediaHeroStyle backdrop, string expected)
    {
        var settings = new AppearanceSettingsDto { MediaPageHero = backdrop };

        SkinClasses.Shell(settings, DeviceType.Desktop).Should().Be(expected);
        SkinClasses.Shell(settings, DeviceType.Tablet).Should().Be(expected);
    }

    [TestCase(MediaHeroSize.Half, "media-pages--size-half")]
    [TestCase(MediaHeroSize.Third, "media-pages--size-third")]
    public void Shell_ShouldCarryTheSizeOfTheBackdrop(MediaHeroSize size, string expected)
    {
        SkinClasses.Shell(new AppearanceSettingsDto { MediaPageHeroSize = size }, DeviceType.Desktop).Should().Be(expected);
    }

    [Test]
    public void Shell_ShouldCombineStyleSizeAndPoster()
    {
        SkinClasses.Shell(new AppearanceSettingsDto { MediaPagePoster = true }, DeviceType.Desktop)
            .Should().Be("media-pages--poster");
        SkinClasses.Shell(
                new AppearanceSettingsDto
                {
                    MediaPageHero = MediaHeroStyle.Banner,
                    MediaPageHeroSize = MediaHeroSize.Third,
                    MediaPagePoster = true
                },
                DeviceType.Desktop)
            .Should().Be("media-pages--hero-banner media-pages--size-third media-pages--poster");
    }

    [Test]
    public void PageGrid_ShouldFollowTheSetting_WhateverTheRest()
    {
        var settings = new AppearanceSettingsDto { MediaPageLists = MediaListStyle.Grid };

        SkinClasses.PageGrid(settings, DeviceType.Desktop).Should().BeTrue();
        SkinClasses.PageGrid(settings with { MediaPageHero = MediaHeroStyle.Cut, MediaPageHeroSize = MediaHeroSize.Half }, DeviceType.Desktop)
            .Should().BeTrue();
    }

    [Test]
    public void PageGridRows_ShouldLimitGrids_OnlyWhenAskedFor()
    {
        var grid = new AppearanceSettingsDto { MediaPageLists = MediaListStyle.Grid };

        SkinClasses.PageGridRows(grid, DeviceType.Desktop).Should().BeNull();
        SkinClasses.PageGridRows(grid with { MediaPageGridMore = true }, DeviceType.Desktop).Should().Be(SkinClasses.GridRowLimit);

        // No grid, nothing to limit.
        SkinClasses.PageGridRows(new AppearanceSettingsDto { MediaPageGridMore = true }, DeviceType.Desktop).Should().BeNull();
    }

    [Test]
    public void MediaPages_ShouldTakeTheSettingsOfTheirKind_WhenItHasItsOwn()
    {
        var settings = new AppearanceSettingsDto
        {
            MediaPageHero = MediaHeroStyle.Banner,
            MediaPageOverrides =
            {
                [MediaPageKind.Series] = new MediaPageSkinDto { HeroSize = MediaHeroSize.Third, Lists = MediaListStyle.Grid, GridMore = true }
            }
        };

        // A kind without settings of its own follows the shared ones, like a page of no kind.
        SkinClasses.Shell(settings, DeviceType.Desktop).Should().Be("media-pages--hero-banner");
        SkinClasses.Shell(settings, DeviceType.Desktop, MediaPageKind.Movie).Should().Be("media-pages--hero-banner");
        SkinClasses.PageGrid(settings, DeviceType.Desktop, MediaPageKind.Movie).Should().BeFalse();

        // Its own settings replace the shared ones as a whole.
        SkinClasses.Shell(settings, DeviceType.Desktop, MediaPageKind.Series).Should().Be("media-pages--size-third");
        SkinClasses.PageGrid(settings, DeviceType.Desktop, MediaPageKind.Series).Should().BeTrue();
        SkinClasses.PageGridRows(settings, DeviceType.Desktop, MediaPageKind.Series).Should().Be(SkinClasses.GridRowLimit);

        SkinClasses.Shell(settings, DeviceType.TV, MediaPageKind.Series).Should().Be("media-pages--size-third");
        SkinClasses.PageGrid(settings, DeviceType.TV, MediaPageKind.Series).Should().BeTrue();
    }

    [Test]
    public void MediaPages_ShouldFollowTheSettingsOnEveryDevice()
    {
        var settings = new AppearanceSettingsDto
        {
            MediaPageHero = MediaHeroStyle.Banner,
            MediaPageHeroSize = MediaHeroSize.Half,
            MediaPagePoster = true,
            MediaPageLists = MediaListStyle.Grid,
            MediaPageGridMore = true
        };
        const string classes = "media-pages--hero-banner media-pages--size-half media-pages--poster";

        foreach (var device in new[] { DeviceType.Desktop, DeviceType.Tablet, DeviceType.Phone, DeviceType.TV })
        {
            SkinClasses.Shell(settings, device).Should().Be(classes);
            SkinClasses.PageGrid(settings, device).Should().BeTrue();
            SkinClasses.PageGridRows(settings, device).Should().Be(SkinClasses.GridRowLimit);
        }
    }

    [Test]
    public void SeasonPage_ShouldBeTheCarouselOnTv_AndAListOrAGridElsewhere()
    {
        var layouts = new[] { SeasonPageLayout.Auto, SeasonPageLayout.List, SeasonPageLayout.Carousel, SeasonPageLayout.Grid };

        // A TV always shows the carousel page, whatever the setting.
        foreach (var layout in layouts)
        {
            var settings = new AppearanceSettingsDto { SeasonPage = layout };
            SkinClasses.SeasonLayout(settings, DeviceType.TV).Should().Be(SeasonPageLayout.Carousel);
            SkinClasses.SeasonImmersive(settings, DeviceType.TV).Should().BeTrue();
        }

        SkinClasses.SeasonLayouts(DeviceType.TV).Should().Equal(SeasonPageLayout.Carousel);

        // The other devices never show it: a list, or a grid when asked.
        foreach (var device in new[] { DeviceType.Desktop, DeviceType.Tablet, DeviceType.Phone })
        {
            foreach (var layout in layouts)
            {
                var settings = new AppearanceSettingsDto { SeasonPage = layout };
                SkinClasses.SeasonLayout(settings, device).Should().Be(layout == SeasonPageLayout.Grid ? SeasonPageLayout.Grid : SeasonPageLayout.List);
                SkinClasses.SeasonImmersive(settings, device).Should().BeFalse();
            }

            SkinClasses.SeasonLayouts(device).Should().Equal(SeasonPageLayout.List, SeasonPageLayout.Grid);
        }

        // The page of a season has no hero for the classes of the other pages.
        SkinClasses.Shell(new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Banner }, DeviceType.Desktop, MediaPageKind.Season)
            .Should().BeNull();
    }

    [Test]
    public void AudioPlayer_ShouldCarryEverySetting()
    {
        var settings = new AppearanceSettingsDto
        {
            AudioBackground = AudioPlayerBackground.Gradient,
            AudioLayout = AudioPlayerLayout.Centered,
            AudioArtwork = AudioArtworkShape.Round,
            AudioAccent = PlayerAccent.Theme
        };

        SkinClasses.AudioPlayer(settings).Split(' ').Should().BeEquivalentTo(
            "fullscreen-player--bg-gradient",
            "fullscreen-player--centered",
            "fullscreen-player--round",
            "fullscreen-player--accent-theme");
    }

    [Test]
    public void AudioPlayer_ShouldMakeTheCoverRound_WhenTheWaveformIsARing()
    {
        var settings = new AppearanceSettingsDto { AudioSeek = AudioSeekStyle.Ring, AudioArtwork = AudioArtworkShape.Square };

        SkinClasses.RoundArtwork(settings).Should().BeTrue();
        SkinClasses.AudioPlayer(settings).Split(' ').Should().BeEquivalentTo(
            "fullscreen-player--round",
            "fullscreen-player--ring");
    }

    [Test]
    public void AudioPlayer_ShouldMarkThePlainRing_OnTopOfTheRing()
    {
        var settings = new AppearanceSettingsDto { AudioSeek = AudioSeekStyle.PlainRing };

        SkinClasses.IsRing(settings).Should().BeTrue();
        SkinClasses.AudioPlayer(settings).Split(' ').Should().BeEquivalentTo(
            "fullscreen-player--round",
            "fullscreen-player--ring",
            "fullscreen-player--ring-plain");
    }

    [Test]
    public void VideoControls_ShouldCarryLayoutCenterAndAccent()
    {
        var settings = new AppearanceSettingsDto
        {
            VideoControls = VideoControlsLayout.Stacked,
            VideoCenterControls = true,
            VideoAccent = PlayerAccent.Theme,
            VideoPoster = true,
            VideoLogo = true
        };

        // Poster and logo are markup, not classes.
        SkinClasses.VideoControls(settings).Split(' ').Should().BeEquivalentTo(
            "video-controls--stacked",
            "video-controls--center",
            "video-controls--accent-theme");
    }
}
