using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class ThemeServiceTests
{
    [Test]
    public void ApplyRemote_ShouldShowAChangeMadeOnAnotherDevice()
    {
        var service = new ThemeService();
        service.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        var changes = 0;
        var remote = 0;
        service.ThemeOnChange += () => changes++;
        service.RemoteApplied += () => remote++;

        var applied = service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "oled" } });

        applied.Should().BeTrue();
        service.Theme.Id.Should().Be("oled");
        // The device type it shows is kept.
        service.DeviceType.Should().Be(DeviceType.TV);
        changes.Should().Be(1);
        remote.Should().Be(1);
    }

    [Test]
    public void ApplyRemote_ShouldIgnoreTheEchoOfAStateThisDeviceAlreadyShowed()
    {
        var service = new ThemeService();
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "paper" } }, DeviceType.Desktop);
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }, DeviceType.Desktop);
        var remote = 0;
        service.RemoteApplied += () => remote++;

        // The server sends back the first change after the second one was made here.
        var applied = service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "paper" } });

        applied.Should().BeFalse();
        service.Theme.Id.Should().Be("oled");
        remote.Should().Be(0);
    }

    [Test]
    public void ApplyRemote_ShouldApplyAStateAgain_WhenAnotherDeviceComesBackToIt()
    {
        var clock = new ManualTimeProvider();
        var service = new ThemeService(clock);
        service.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        clock.Advance(TimeSpan.FromMinutes(1));

        // A setting switched on, then off again, on another device.
        service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }).Should().BeTrue();
        service.ApplyRemote(new AppearancePreferencesDto()).Should().BeTrue();
        service.Settings.ThemeId.Should().BeNull();

        service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }).Should().BeTrue();
        service.Settings.ThemeId.Should().Be("oled");
    }

    [Test]
    public void ApplyRemote_ShouldOnlyTakeAStateForAnEcho_RightAfterThisDeviceShowedIt()
    {
        var clock = new ManualTimeProvider();
        var service = new ThemeService(clock);
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "paper" } }, DeviceType.Desktop);
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }, DeviceType.Desktop);

        // Long after, another device picks the theme this one showed once: a real change.
        clock.Advance(TimeSpan.FromMinutes(1));

        service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "paper" } }).Should().BeTrue();
        service.Theme.Id.Should().Be("paper");
    }

    [Test]
    public void ApplyRemote_ShouldDoNothing_WhenTheStateIsAlreadyOnScreen()
    {
        var clock = new ManualTimeProvider();
        var service = new ThemeService(clock);
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }, DeviceType.Desktop);
        clock.Advance(TimeSpan.FromMinutes(1));
        var remote = 0;
        service.RemoteApplied += () => remote++;

        service.ApplyRemote(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }).Should().BeFalse();
        remote.Should().Be(0);
    }

    [Test]
    public void Settings_ShouldTakeTheSkinsOfTheServer_ForThePartsTheUserDidNotSetUp()
    {
        var service = new ThemeService();
        service.SetServerDefaultSkins(new AppearanceSettingsDto
        {
            MediaPageHero = MediaHeroStyle.Banner,
            AudioLayout = AudioPlayerLayout.Centered,
            VideoLogo = true
        });

        // Nothing set up: everything comes from the server.
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "oled" } }, DeviceType.Desktop);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Banner);
        service.Settings.AudioLayout.Should().Be(AudioPlayerLayout.Centered);
        service.Settings.VideoLogo.Should().BeTrue();
        // The look stays the one of the user.
        service.Settings.ThemeId.Should().Be("oled");

        // The user set up their media pages: that part is theirs as a whole, the players still follow.
        service.Apply(new AppearancePreferencesDto { Default = { MediaPagePoster = true } }, DeviceType.Desktop);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Fade);
        service.Settings.MediaPagePoster.Should().BeTrue();
        service.Settings.AudioLayout.Should().Be(AudioPlayerLayout.Centered);

        // Choosing the built-in values is a choice too.
        service.Apply(new AppearancePreferencesDto { Default = { MediaPagesOwn = true } }, DeviceType.Desktop);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Fade);

        // No defaults on the server: the settings of the user as they are.
        var changes = 0;
        service.ThemeOnChange += () => changes++;
        service.SetServerDefaultSkins(null);
        changes.Should().Be(1);
        service.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Fade);
        service.Settings.AudioLayout.Should().Be(AudioPlayerLayout.Side);
    }

    [Test]
    public void Settings_ShouldTakeTheSkinsOfTheServerForItsDeviceType_WhenThatTypeHasItsOwn()
    {
        var service = new ThemeService();
        service.SetServerSkins(new ServerSkinsDto
        {
            Default = { MediaPageHero = MediaHeroStyle.Banner },
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { MediaPageHero = MediaHeroStyle.Cut } }
        });

        service.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Cut);

        // A type of device without a set of its own shows the shared one.
        service.Apply(new AppearancePreferencesDto(), DeviceType.Phone);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Banner);

        // An account that set up its media pages keeps them on every device.
        service.Apply(new AppearancePreferencesDto { Default = { MediaPagesOwn = true } }, DeviceType.TV);
        service.Settings.MediaPageHero.Should().Be(MediaHeroStyle.Fade);
    }

    [Test]
    public void Look_ShouldTakeTheThemeAndCornersOfTheServerForItsDeviceType_UntilTheUserPicksTheirs()
    {
        var service = new ThemeService();
        service.SetServerDefault("midnight");
        service.SetServerSkins(new ServerSkinsDto
        {
            Default = { CornerStyle = AppearanceCornerStyle.Round },
            DeviceOverrides =
            {
                [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "oled", CornerStyle = AppearanceCornerStyle.Square },
                // No theme of its own: the shared one.
                [DeviceType.Tablet] = new AppearanceSettingsDto { CornerStyle = AppearanceCornerStyle.Square }
            }
        });

        service.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        service.Theme.Id.Should().Be("oled");
        service.Settings.CornerStyle.Should().Be(AppearanceCornerStyle.Square);

        service.Apply(new AppearancePreferencesDto(), DeviceType.Tablet);
        service.Theme.Id.Should().Be("midnight");
        service.Settings.CornerStyle.Should().Be(AppearanceCornerStyle.Square);

        service.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);
        service.Theme.Id.Should().Be("midnight");
        service.Settings.CornerStyle.Should().Be(AppearanceCornerStyle.Round);

        // The user picked a theme and the standard corners: both are theirs.
        service.Apply(
            new AppearancePreferencesDto { Default = { ThemeId = "paper", CornerStyleOwn = true } },
            DeviceType.TV);
        service.Theme.Id.Should().Be("paper");
        service.Settings.CornerStyle.Should().Be(AppearanceCornerStyle.Default);

        // A theme the server no longer has falls back on the shared one.
        service.SetServerSkins(new ServerSkinsDto
        {
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "server-gone" } }
        });
        service.Apply(new AppearancePreferencesDto(), DeviceType.TV);
        service.Theme.Id.Should().Be("midnight");
    }

    [Test]
    public void Theme_ShouldFollowServerDefault_WhenUserPickedNothing()
    {
        var service = new ThemeService();
        service.SetServerDefault("midnight");

        service.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);

        service.Theme.Id.Should().Be("midnight");
    }

    [Test]
    public void Settings_ShouldUseDeviceOverride_WhenOneExistsForThisDeviceType()
    {
        var service = new ThemeService();
        var preferences = new AppearancePreferencesDto
        {
            Default = { ThemeId = "paper" },
            DeviceOverrides = { [DeviceType.TV] = new AppearanceSettingsDto { ThemeId = "oled", TextSize = AppearanceTextSize.Large } }
        };

        service.Apply(preferences, DeviceType.TV);
        service.Theme.Id.Should().Be("oled");
        service.Settings.TextSize.Should().Be(AppearanceTextSize.Large);

        service.Apply(preferences, DeviceType.Phone);
        service.Theme.Id.Should().Be("paper");
    }

    [Test]
    public void Theme_ShouldResolveCustomTheme_WhenIdMatchesOneOfTheUsersThemes()
    {
        var service = new ThemeService();
        var custom = new CustomThemeDto { Id = "custom-1", Name = "Mine", Palette = BuiltInThemes.Forest.Palette with { } };

        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "custom-1" }, CustomThemes = { custom } }, DeviceType.Desktop);

        service.Theme.IsBuiltIn.Should().BeFalse();
        service.Theme.CustomName.Should().Be("Mine");
    }

    [Test]
    public void Theme_ShouldFallBackToServerDefault_WhenThemeNoLongerExists()
    {
        var service = new ThemeService();

        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "custom-deleted" } }, DeviceType.Desktop);

        service.Theme.Id.Should().Be(BuiltInThemes.DefaultDarkId);
    }

    [Test]
    public void SetPreview_ShouldShowPreviewWithoutAccentOverride_UntilCleared()
    {
        var service = new ThemeService();
        service.Apply(new AppearancePreferencesDto { Default = { AccentColor = "#ff0000" } }, DeviceType.Desktop);
        var changes = 0;
        service.ThemeOnChange += () => changes++;

        service.SetPreview(BuiltInThemes.Plum);

        service.Theme.Id.Should().Be("plum");
        service.Css.Declarations.Should().Contain("--color-accent:#d48fd0;");
        changes.Should().Be(1);

        service.SetPreview(null);

        service.Theme.Id.Should().Be(BuiltInThemes.DefaultDarkId);
        service.Css.Declarations.Should().Contain("--color-accent:#ff0000;");
    }

    [Test]
    public void IsResolved_ShouldStayFalse_UntilAnAppearanceIsApplied()
    {
        var service = new ThemeService();
        service.SetServerDefault("paper");

        service.IsResolved.Should().BeFalse();

        service.ApplyServerDefault();

        service.IsResolved.Should().BeTrue();
        service.Theme.Id.Should().Be("paper");
    }

    private static CustomThemeDto ServerTheme(string id = "server-house") => new()
    {
        Id = id,
        Name = "House",
        Palette = BuiltInThemes.Forest.Palette with { }
    };

    [Test]
    public void Theme_ShouldBeTheServerTheme_WhenItIsTheServerDefault()
    {
        var service = new ThemeService();
        service.SetServerDefault("server-house", [ServerTheme()]);

        service.Apply(new AppearancePreferencesDto(), DeviceType.Desktop);

        service.Theme.Id.Should().Be("server-house");
        service.Theme.CustomName.Should().Be("House");
    }

    [Test]
    public void Theme_ShouldBeTheServerTheme_WhenTheUserPickedIt()
    {
        var service = new ThemeService();
        service.SetServerDefault("midnight", [ServerTheme()]);

        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "server-house" } }, DeviceType.Desktop);

        service.Theme.Id.Should().Be("server-house");
    }

    [Test]
    public void Theme_ShouldFallBackToServerDefault_WhenThePickedServerThemeWasDeleted()
    {
        var service = new ThemeService();
        service.SetServerDefault("midnight", [ServerTheme()]);
        service.Apply(new AppearancePreferencesDto { Default = { ThemeId = "server-house" } }, DeviceType.Desktop);

        service.SetServerDefault("midnight", []);

        service.Theme.Id.Should().Be("midnight");
    }

    [Test]
    public void SetServerDefault_ShouldFallBackToDarkBase_WhenDefaultIsAnUnknownServerTheme()
    {
        var service = new ThemeService();

        service.SetServerDefault("server-gone", [ServerTheme()]);

        service.ServerDefaultThemeId.Should().Be(BuiltInThemes.DefaultDarkId);
    }

    [Test]
    public void SetServerDefault_ShouldNotify_WhenOnlyAServerThemeChanged()
    {
        var service = new ThemeService();
        service.SetServerDefault("server-house", [ServerTheme()]);
        service.ApplyServerDefault();
        var changes = 0;
        service.ThemeOnChange += () => changes++;

        var edited = ServerTheme();
        edited.Palette.Accent = "#ff8800";
        service.SetServerDefault("server-house", [edited]);
        service.SetServerDefault("server-house", [edited]);

        changes.Should().Be(1);
        service.Theme.Palette.Accent.Should().Be("#ff8800");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
