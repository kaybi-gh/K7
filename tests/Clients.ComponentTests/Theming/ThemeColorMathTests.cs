using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class ThemeColorMathTests
{
    [TestCase("#000000", true)]
    [TestCase("#D9b060", true)]
    [TestCase("#fff", false)]
    [TestCase("d9b060", false)]
    [TestCase("#d9b06g", false)]
    [TestCase("#d9b060;", false)]
    [TestCase(null, false)]
    public void IsHex_ShouldAcceptOnlySixDigitHex(string? value, bool expected)
    {
        ThemeColorMath.IsHex(value).Should().Be(expected);
    }

    [Test]
    public void ContrastRatio_ShouldBeTwentyOne_WhenBlackOnWhite()
    {
        ThemeColorMath.ContrastRatio("#000000", "#ffffff").Should().BeApproximately(21, 0.01);
    }

    [Test]
    public void ReadableInkOn_ShouldPickDarkInk_WhenFillIsLight()
    {
        ThemeColorMath.ReadableInkOn("#f5e663").Should().Be(ThemeColorMath.DarkInk);
        ThemeColorMath.ReadableInkOn("#1f5288").Should().Be(ThemeColorMath.LightInk);
    }

    [Test]
    public void EnsureContrast_ShouldReachMinimumRatio_WhenColorIsTooCloseToBackground()
    {
        var adjusted = ThemeColorMath.EnsureContrast("#f5e663", "#e4e3d9", ThemePaletteValidator.TextRatio);

        ThemeColorMath.ContrastRatio(adjusted, "#e4e3d9").Should().BeGreaterThanOrEqualTo(ThemePaletteValidator.TextRatio);
    }

    [Test]
    public void EnsureContrast_ShouldKeepColor_WhenAlreadyReadable()
    {
        ThemeColorMath.EnsureContrast("#D9B060", "#0d0907", ThemePaletteValidator.TextRatio).Should().Be("#d9b060");
    }
}
