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
