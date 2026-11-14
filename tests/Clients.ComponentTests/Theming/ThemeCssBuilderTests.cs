using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class ThemeCssBuilderTests
{
    [Test]
    public void Build_ShouldEmitNothing_WhenBaseThemeHasDefaultSettings()
    {
        var css = ThemeCssBuilder.Build(BuiltInThemes.DefaultDark, new AppearanceSettingsDto());

        css.BaseAttribute.Should().Be(BuiltInThemes.DefaultDarkId);
        css.Declarations.Should().BeEmpty();
        css.ThemeColor.Should().Be("#0d0907");
    }

    [Test]
    public void Build_ShouldEmitPaletteOverLightBase_WhenThemeIsLight()
    {
        var css = ThemeCssBuilder.Build(BuiltInThemes.Paper, new AppearanceSettingsDto());

        css.BaseAttribute.Should().Be(BuiltInThemes.DefaultLightId);
        css.Declarations.Should().Contain("--color-bg:#f4f1ea;");
        css.Declarations.Should().Contain("--color-accent:#a85a14;");
        css.Declarations.Should().Contain("--color-accent-text:#7a3f0b;");
        css.ThemeColor.Should().Be("#f4f1ea");
    }

    [Test]
    public void Build_ShouldDeriveReadableAccent_WhenAccentIsOverriddenOnBaseTheme()
    {
        var settings = new AppearanceSettingsDto { AccentColor = "#F5E663" };

        var css = ThemeCssBuilder.Build(BuiltInThemes.DefaultLight, settings);

        css.Declarations.Should().Contain("--color-accent:#f5e663;");
        css.Declarations.Should().Contain($"--color-accent-fg:{ThemeColorMath.DarkInk};");
        css.Declarations.Should().NotContain("--color-bg:");
        css.Declarations.Should().NotContain("--color-accent-text:#f5e663;", "a pale accent is darkened for text on a light background");
    }

    [Test]
    public void Build_ShouldIgnoreAccent_WhenOverrideIsNotAHexColor()
    {
        var settings = new AppearanceSettingsDto { AccentColor = "red;} body{display:none" };

        var css = ThemeCssBuilder.Build(BuiltInThemes.DefaultDark, settings);

        css.Declarations.Should().BeEmpty();
    }

    [Test]
    public void Build_ShouldFallBackToBase_WhenPaletteHasInvalidColor()
    {
        var palette = BuiltInThemes.Midnight.Palette with { Background = "url(x)" };
        var theme = new ThemeDefinition("custom-bad", palette, IsBuiltIn: false);

        var css = ThemeCssBuilder.Build(theme, new AppearanceSettingsDto());

        css.Declarations.Should().BeEmpty();
        css.ThemeColor.Should().Be("#0d0907");
    }

    [Test]
    public void Build_ShouldEmitRadiusAndTextSize_WhenSettingsAreSet()
    {
        var settings = new AppearanceSettingsDto
        {
            TextSize = AppearanceTextSize.Large,
            CornerStyle = AppearanceCornerStyle.Square
        };

        var css = ThemeCssBuilder.Build(BuiltInThemes.DefaultDark, settings);

        css.Declarations.Should().Contain("--radius-md:2px;");
        css.Declarations.Should().Contain("--k7-text-scale:1.125;");
    }

    [Test]
    public void Build_ShouldScaleTextWithAFactor_NotAFontSize()
    {
        // A font size on the root would replace the larger base size of the TV layout.
        var settings = new AppearanceSettingsDto { TextSize = AppearanceTextSize.ExtraLarge };

        var css = ThemeCssBuilder.Build(BuiltInThemes.DefaultDark, settings);

        css.Declarations.Should().Contain("--k7-text-scale:1.25;");
        css.Declarations.Should().NotContain("font-size");
    }
}
