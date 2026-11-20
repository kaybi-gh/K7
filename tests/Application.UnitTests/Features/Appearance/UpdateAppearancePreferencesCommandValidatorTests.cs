using K7.Server.Application.Features.Appearance.Commands.UpdateAppearancePreferences;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Server.Application.UnitTests.Features.Appearance;

[TestFixture]
public class UpdateAppearancePreferencesCommandValidatorTests
{
    private readonly UpdateAppearancePreferencesCommandValidator _validator = new();

    private static CustomThemeDto CustomTheme(string id = "custom-abc") => new()
    {
        Id = id,
        Name = "Mine",
        Palette = BuiltInThemes.Midnight.Palette with { }
    };

    private bool IsValid(AppearancePreferencesDto preferences) =>
        _validator.Validate(new UpdateAppearancePreferencesCommand { Preferences = preferences }).IsValid;

    [Test]
    public void Validate_ShouldPass_WhenPreferencesAreDefault()
    {
        IsValid(new AppearancePreferencesDto()).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldPass_WhenThemeAccentOverrideAndCustomThemeAreValid()
    {
        var preferences = new AppearancePreferencesDto
        {
            Default = { ThemeId = "custom-abc", AccentColor = "#12ab34", TextSize = AppearanceTextSize.Large },
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "oled" } },
            CustomThemes = { CustomTheme() }
        };

        IsValid(preferences).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldPass_WhenCardStylesAreSetOrFollowTheServer()
    {
        var cards = new MediaCardPreferencesDto
        {
            Default =
            {
                ShowSubtitle = false,
                Details = [CardDetailField.Year, CardDetailField.Rating],
                DetailsPlacement = CardDetailPlacement.Hover,
                Hover = CardHoverStyle.Lift
            },
            Overrides = { [CardMediaGroup.Music] = new MediaCardStyleDto { ShowTitle = false } }
        };

        IsValid(new AppearancePreferencesDto { Cards = cards }).Should().BeTrue();
        IsValid(new AppearancePreferencesDto { Cards = null }).Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldFail_WhenACardStyleIsNotValid()
    {
        IsValid(new AppearancePreferencesDto
        {
            Cards = new MediaCardPreferencesDto { Default = { Details = [(CardDetailField)99] } }
        }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto
        {
            Cards = new MediaCardPreferencesDto { Default = { Hover = (CardHoverStyle)99 } }
        }).Should().BeFalse();
    }

    [Test]
    public void NeedsDetailedFeed_ShouldBeTrue_OnlyWhenAStyleShowsMoreThanTheYear()
    {
        new MediaCardPreferencesDto().NeedsDetailedFeed.Should().BeFalse();
        new MediaCardPreferencesDto { Default = { Details = [CardDetailField.Year] } }.NeedsDetailedFeed.Should().BeFalse();
        new MediaCardPreferencesDto
        {
            Overrides = { [CardMediaGroup.Movies] = new MediaCardStyleDto { Details = [CardDetailField.Runtime] } }
        }.NeedsDetailedFeed.Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldCheckTheMiniPlayerPlacement()
    {
        IsValid(new AppearancePreferencesDto { Default = { MiniPlayer = MiniPlayerPlacement.Bottom } }).Should().BeTrue();
        IsValid(new AppearancePreferencesDto { Default = { MiniPlayer = (MiniPlayerPlacement)99 } }).Should().BeFalse();
        new AppearanceSettingsDto { MiniPlayer = MiniPlayerPlacement.Bottom }.IsDefault.Should().BeFalse();
    }

    [Test]
    public void ServerSkins_ShouldKeepASetPerDeviceType_AndReadTheFormerSingleSet()
    {
        var skins = new ServerSkinsDto
        {
            Default = { MediaPageHero = MediaHeroStyle.Banner, ThemeId = "oled" },
            DeviceOverrides =
            {
                [DeviceType.TV] = new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Cut },
                // Same as the shared set: not a set of its own.
                [DeviceType.Phone] = new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Banner },
                // The built-in pages are a choice when the shared set is something else.
                [DeviceType.Tablet] = new AppearanceSettingsDto()
            }
        };

        var read = AppearanceSkins.ParseSet(AppearanceSkins.Serialize(skins))!;

        read.Default.MediaPageHero.Should().Be(MediaHeroStyle.Banner);
        read.Default.ThemeId.Should().BeNull("only the skins are stored");
        read.DeviceOverrides.Keys.Should().BeEquivalentTo([DeviceType.TV, DeviceType.Tablet]);
        read.Resolve(DeviceType.TV).MediaPageHero.Should().Be(MediaHeroStyle.Cut);
        read.Resolve(DeviceType.Tablet).MediaPageHero.Should().Be(MediaHeroStyle.Fade);
        read.Resolve(DeviceType.Phone).MediaPageHero.Should().Be(MediaHeroStyle.Banner);
        read.Resolve(DeviceType.Desktop).MediaPageHero.Should().Be(MediaHeroStyle.Banner);

        // What a server stored before device types had their own set: the shared set alone.
        var former = AppearanceSkins.ParseSet(AppearanceSkins.Serialize(new AppearanceSettingsDto { AudioLayout = AudioPlayerLayout.Centered }))!;
        former.Default.AudioLayout.Should().Be(AudioPlayerLayout.Centered);
        former.DeviceOverrides.Should().BeEmpty();

        AppearanceSkins.ParseSet(AppearanceSkins.Serialize(new ServerSkinsDto())).Should().BeNull();
        AppearanceSkins.ParseSet(null).Should().BeNull();
        AppearanceSkins.ParseSet("not json").Should().BeNull();
        AppearanceSkins.ParseSet("[]").Should().BeNull();
    }

    [Test]
    public void ServerSkins_ShouldKeepCornersAndTheThemeOfADeviceType_ButNotTheSharedTheme()
    {
        var skins = new ServerSkinsDto
        {
            // The shared theme is the default theme of the server, stored on its own.
            Default = { ThemeId = "midnight", CornerStyle = AppearanceCornerStyle.Round, AccentColor = "#ff0000", TextSize = AppearanceTextSize.Large },
            DeviceOverrides =
            {
                [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "oled", CornerStyle = AppearanceCornerStyle.Round },
                // The shared theme and other corners: it follows the shared theme.
                [DeviceType.Phone] = new AppearanceSettingsDto { ThemeId = "midnight", CornerStyle = AppearanceCornerStyle.Square },
                // The shared theme and the shared corners: not a set of its own.
                [DeviceType.Tablet] = new AppearanceSettingsDto { ThemeId = "midnight", CornerStyle = AppearanceCornerStyle.Round }
            }
        };

        var read = AppearanceSkins.ParseSet(AppearanceSkins.Serialize(skins))!;

        read.Default.ThemeId.Should().BeNull();
        read.Default.CornerStyle.Should().Be(AppearanceCornerStyle.Round);
        read.Default.AccentColor.Should().BeNull("the server sets no accent");
        read.Default.TextSize.Should().Be(AppearanceTextSize.Default, "the text size is for each user to choose");
        read.DeviceOverrides.Keys.Should().BeEquivalentTo([DeviceType.TV, DeviceType.Phone]);
        read.DeviceOverrides[DeviceType.TV].ThemeId.Should().Be("oled");
        read.DeviceOverrides[DeviceType.Phone].ThemeId.Should().BeNull();
        read.DeviceOverrides[DeviceType.Phone].CornerStyle.Should().Be(AppearanceCornerStyle.Square);

        // Corners alone are worth storing.
        AppearanceSkins.ParseSet(AppearanceSkins.Serialize(new ServerSkinsDto { Default = { CornerStyle = AppearanceCornerStyle.Square } }))
            .Should().NotBeNull();

        // The theme of a device type is built-in or of the server, never the theme of an account.
        AppearanceSkins.IsValid(new ServerSkinsDto
        {
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "custom-abc" } }
        }).Should().BeFalse();
    }

    [Test]
    public void Settings_ShouldFollowTheCornersOfTheServer_UntilTheUserPicksTheirs()
    {
        var server = new AppearanceSettingsDto { CornerStyle = AppearanceCornerStyle.Round };

        new AppearanceSettingsDto().WithServerSkins(server).CornerStyle.Should().Be(AppearanceCornerStyle.Round);
        new AppearanceSettingsDto { CornerStyle = AppearanceCornerStyle.Square }.WithServerSkins(server).CornerStyle
            .Should().Be(AppearanceCornerStyle.Square);

        // The standard corners, picked: a choice, not "follow the server".
        var own = new AppearanceSettingsDto { CornerStyleOwn = true };
        own.WithServerSkins(server).CornerStyle.Should().Be(AppearanceCornerStyle.Default);
        own.IsLookDefault.Should().BeFalse();
    }

    [Test]
    public void ServerSkins_ShouldRejectASetForNoDeviceTypeOrWithInvalidValues()
    {
        AppearanceSkins.IsValid(new ServerSkinsDto
        {
            DeviceOverrides = { [DeviceType.Unknown] = new AppearanceSettingsDto() }
        }).Should().BeFalse();
        AppearanceSkins.IsValid(new ServerSkinsDto
        {
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { SeasonPage = (SeasonPageLayout)99 } }
        }).Should().BeFalse();
        AppearanceSkins.IsValid(new ServerSkinsDto { Default = { AudioSeek = (AudioSeekStyle)99 } }).Should().BeFalse();
        AppearanceSkins.IsValid((ServerSkinsDto?)null).Should().BeFalse();
    }

    [Test]
    public void Settings_ShouldTreatTheMiniPlayerAsPartOfTheMusicPlayer()
    {
        // Set up on the music player page: it follows the server defaults with it, and
        // resetting the look does not move it.
        var server = new AppearanceSettingsDto { MiniPlayer = MiniPlayerPlacement.Bottom };

        new AppearanceSettingsDto().WithServerSkins(server).MiniPlayer.Should().Be(MiniPlayerPlacement.Bottom);
        AppearanceSkins.Parse(AppearanceSkins.Serialize(server))!.MiniPlayer.Should().Be(MiniPlayerPlacement.Bottom);
        AppearanceSkins.IsValid(new AppearanceSettingsDto { MiniPlayer = (MiniPlayerPlacement)99 }).Should().BeFalse();

        var ownBar = new AppearanceSettingsDto { AudioPlayerOwn = true };
        ownBar.WithServerSkins(server).MiniPlayer.Should().Be(MiniPlayerPlacement.Auto, "an account that set up its music player keeps its choice");

        var own = new AppearanceSettingsDto { MiniPlayer = MiniPlayerPlacement.Bottom };
        own.IsLookDefault.Should().BeTrue();
        own.IsAudioPlayerDefault.Should().BeFalse();

        own.ResetAudioPlayer();
        own.MiniPlayer.Should().Be(MiniPlayerPlacement.Auto);
        own.IsAudioPlayerDefault.Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldCheckTheSkinsOfPagesAndPlayers()
    {
        IsValid(new AppearancePreferencesDto
        {
            Default =
            {
                MediaPageHero = MediaHeroStyle.Banner,
                MediaPageHeroSize = MediaHeroSize.Third,
                MediaPagePoster = true,
                MediaPageLists = MediaListStyle.Grid,
                MediaPageGridMore = true,
                AudioBackground = AudioPlayerBackground.Gradient,
                AudioLayout = AudioPlayerLayout.Centered,
                AudioArtwork = AudioArtworkShape.Round,
                AudioSeek = AudioSeekStyle.Ring,
                AudioAccent = PlayerAccent.Theme,
                VideoControls = VideoControlsLayout.Stacked,
                VideoAccent = PlayerAccent.Theme
            }
        }).Should().BeTrue();

        IsValid(new AppearancePreferencesDto { Default = { MediaPageHero = (MediaHeroStyle)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { MediaPageHeroSize = (MediaHeroSize)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { MediaPageLists = (MediaListStyle)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { SeasonPage = SeasonPageLayout.Carousel } }).Should().BeTrue();
        IsValid(new AppearancePreferencesDto { Default = { SeasonPage = (SeasonPageLayout)99 } }).Should().BeFalse();
        new AppearanceSettingsDto { SeasonPage = SeasonPageLayout.List }.IsMediaPagesDefault.Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { AudioBackground = (AudioPlayerBackground)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { AudioLayout = (AudioPlayerLayout)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { AudioArtwork = (AudioArtworkShape)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { AudioSeek = (AudioSeekStyle)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { AudioAccent = (PlayerAccent)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { VideoControls = (VideoControlsLayout)99 } }).Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { VideoAccent = (PlayerAccent)99 } }).Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldCheckTheSettingsOfEachKindOfMediaPage()
    {
        IsValid(new AppearancePreferencesDto
        {
            Default =
            {
                MediaPageOverrides =
                {
                    [MediaPageKind.Series] = new MediaPageSkinDto { Hero = MediaHeroStyle.Cut, HeroSize = MediaHeroSize.Third, Lists = MediaListStyle.Grid }
                }
            }
        }).Should().BeTrue();

        IsValid(new AppearancePreferencesDto { Default = { MediaPageOverrides = { [(MediaPageKind)99] = new MediaPageSkinDto() } } })
            .Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { MediaPageOverrides = { [MediaPageKind.Movie] = new MediaPageSkinDto { Hero = (MediaHeroStyle)99 } } } })
            .Should().BeFalse();
        IsValid(new AppearancePreferencesDto { Default = { MediaPageOverrides = { [MediaPageKind.Movie] = new MediaPageSkinDto { HeroSize = (MediaHeroSize)99 } } } })
            .Should().BeFalse();
    }

    [Test]
    public void Settings_ShouldGiveAKindOfMediaPageItsOwnSettings_AndCopyThemApart()
    {
        var settings = new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Banner, MediaPagePoster = true };

        settings.PageSkin(MediaPageKind.Series).Should().Be(settings.SharedPageSkin());
        settings.IsMediaPagesDefault.Should().BeFalse();

        settings.MediaPageOverrides[MediaPageKind.Series] = new MediaPageSkinDto { HeroSize = MediaHeroSize.Half };
        settings.PageSkin(MediaPageKind.Series).HeroSize.Should().Be(MediaHeroSize.Half);
        settings.PageSkin(MediaPageKind.Series).Hero.Should().Be(MediaHeroStyle.Fade);
        settings.PageSkin(MediaPageKind.Movie).Hero.Should().Be(MediaHeroStyle.Banner);
        settings.PageSkin().Hero.Should().Be(MediaHeroStyle.Banner);

        // A copy can change without touching the original.
        var copy = settings.Copy();
        copy.MediaPageOverrides[MediaPageKind.Series].HeroSize = MediaHeroSize.Third;
        copy.MediaPageOverrides.Remove(MediaPageKind.Series);
        settings.PageSkin(MediaPageKind.Series).HeroSize.Should().Be(MediaHeroSize.Half);

        settings.ResetMediaPages();
        settings.IsMediaPagesDefault.Should().BeTrue();
        settings.MediaPageOverrides.Should().BeEmpty();
    }

    [Test]
    public void Skins_ShouldBeStoredAlone_AndReadBackOnlyWhenTheyAreSet()
    {
        var settings = new AppearanceSettingsDto
        {
            ThemeId = "oled",
            MediaPageHero = MediaHeroStyle.Cut,
            SeasonPage = SeasonPageLayout.Grid,
            MediaPageOverrides = { [MediaPageKind.Series] = new MediaPageSkinDto { HeroSize = MediaHeroSize.Half } },
            AudioSeek = AudioSeekStyle.Ring,
            VideoLogo = true
        };

        var read = AppearanceSkins.Parse(AppearanceSkins.Serialize(settings));

        read.Should().NotBeNull();
        read!.MediaPageHero.Should().Be(MediaHeroStyle.Cut);
        read.SeasonPage.Should().Be(SeasonPageLayout.Grid);
        read.PageSkin(MediaPageKind.Series).HeroSize.Should().Be(MediaHeroSize.Half);
        read.AudioSeek.Should().Be(AudioSeekStyle.Ring);
        read.VideoLogo.Should().BeTrue();
        // The look of the administrator does not come along.
        read.ThemeId.Should().BeNull();

        AppearanceSkins.Parse(AppearanceSkins.Serialize(new AppearanceSettingsDto { ThemeId = "oled" })).Should().BeNull();
        AppearanceSkins.Parse(null).Should().BeNull();
        AppearanceSkins.Parse("not json").Should().BeNull();
        AppearanceSkins.IsValid(new AppearanceSettingsDto { SeasonPage = (SeasonPageLayout)99 }).Should().BeFalse();
    }

    [Test]
    public void Settings_ShouldFollowTheServer_UntilThePartIsSetUp()
    {
        var server = new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Banner, AudioLayout = AudioPlayerLayout.Centered };

        var untouched = new AppearanceSettingsDto();
        untouched.WithServerSkins(null).Should().BeSameAs(untouched);
        untouched.WithServerSkins(server).MediaPageHero.Should().Be(MediaHeroStyle.Banner);
        untouched.IsMediaPagesDefault.Should().BeTrue();

        var own = new AppearanceSettingsDto { MediaPagesOwn = true };
        own.IsMediaPagesDefault.Should().BeFalse();
        own.WithServerSkins(server).MediaPageHero.Should().Be(MediaHeroStyle.Fade);
        own.WithServerSkins(server).AudioLayout.Should().Be(AudioPlayerLayout.Centered);

        own.ResetMediaPages();
        own.IsMediaPagesDefault.Should().BeTrue();
        own.WithServerSkins(server).MediaPageHero.Should().Be(MediaHeroStyle.Banner);
    }

    [Test]
    public void Settings_ShouldTellWhichPartLeftItsDefaults_AndResetItAlone()
    {
        var settings = new AppearanceSettingsDto { ThemeId = "graphite", MediaPagePoster = true, VideoPoster = true, AudioSeek = AudioSeekStyle.Ring };

        settings.IsLookDefault.Should().BeFalse();
        settings.IsMediaPagesDefault.Should().BeFalse();
        settings.IsPlayersDefault.Should().BeFalse();

        settings.ResetAudioPlayer();
        settings.IsAudioPlayerDefault.Should().BeTrue();
        settings.IsVideoPlayerDefault.Should().BeFalse();

        settings.ResetVideoPlayer();
        settings.IsPlayersDefault.Should().BeTrue();
        settings.IsMediaPagesDefault.Should().BeFalse();

        settings.ResetMediaPages();
        settings.IsMediaPagesDefault.Should().BeTrue();
        settings.ThemeId.Should().Be("graphite");
        settings.IsDefault.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldPass_WhenThemeIsAServerTheme()
    {
        IsValid(new AppearancePreferencesDto { Default = { ThemeId = "server-abc123" } }).Should().BeTrue();
    }

    [TestCase("not-a-theme")]
    [TestCase("server-")]
    [TestCase("custom-")]
    [TestCase("custom-<script>")]
    public void Validate_ShouldFail_WhenThemeIdIsNotBuiltInNorCustom(string themeId)
    {
        IsValid(new AppearancePreferencesDto { Default = { ThemeId = themeId } }).Should().BeFalse();
    }

    [TestCase("red")]
    [TestCase("#fff")]
    [TestCase("#12ab34;color:red")]
    public void Validate_ShouldFail_WhenAccentIsNotAHexColor(string accent)
    {
        IsValid(new AppearancePreferencesDto { Default = { AccentColor = accent } }).Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenCustomThemeHasInvalidColor()
    {
        var theme = CustomTheme();
        theme.Palette.Background = "url(https://example.test/x)";

        IsValid(new AppearancePreferencesDto { CustomThemes = { theme } }).Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenCustomThemeIdsAreDuplicated()
    {
        IsValid(new AppearancePreferencesDto { CustomThemes = { CustomTheme(), CustomTheme() } }).Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenTooManyCustomThemes()
    {
        var preferences = new AppearancePreferencesDto();
        for (var i = 0; i <= AppearancePreferencesDto.MaxCustomThemes; i++)
            preferences.CustomThemes.Add(CustomTheme($"custom-{i}"));

        IsValid(preferences).Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenEnumValueIsOutOfRange()
    {
        IsValid(new AppearancePreferencesDto { Default = { CornerStyle = (AppearanceCornerStyle)42 } }).Should().BeFalse();
    }
}
