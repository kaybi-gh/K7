using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class ThemeServiceTests
{
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
}
