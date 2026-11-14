using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class ThemeFileTests
{
    private static CustomThemeDto Sample() => new()
    {
        Id = "custom-sample",
        Name = "Sample",
        Palette = BuiltInThemes.Midnight.Palette with { }
    };

    [Test]
    public void TryParse_ShouldRoundTripNameAndPalette_WhenFileWasSerialized()
    {
        var json = ThemeFile.Serialize(Sample());

        var error = ThemeFile.TryParse(json, out var theme);

        error.Should().Be(ThemeFileError.None);
        theme!.Name.Should().Be("Sample");
        theme.Palette.Should().Be(BuiltInThemes.Midnight.Palette);
        theme.Id.Should().NotBe("custom-sample", "an imported theme gets its own id");
        ThemePaletteValidator.IsValidCustomThemeId(theme.Id).Should().BeTrue();
    }

    [TestCase("", ThemeFileError.Empty)]
    [TestCase("not json", ThemeFileError.InvalidJson)]
    [TestCase("{\"format\":\"other\",\"version\":1,\"name\":\"x\",\"palette\":{}}", ThemeFileError.InvalidFormat)]
    [TestCase("{\"format\":\"k7-theme\",\"version\":1,\"name\":\"x\"}", ThemeFileError.InvalidFormat)]
    public void TryParse_ShouldReportError_WhenContentIsNotATheme(string json, ThemeFileError expected)
    {
        ThemeFile.TryParse(json, out var theme).Should().Be(expected);
        theme.Should().BeNull();
    }

    [Test]
    public void TryParse_ShouldRejectFile_WhenAColorIsNotHex()
    {
        var json = ThemeFile.Serialize(Sample()).Replace("#0a0e1a", "red;}*{display:none");

        ThemeFile.TryParse(json, out _).Should().Be(ThemeFileError.InvalidColors);
    }

    [Test]
    public void TryParse_ShouldRejectFile_WhenContentIsTooLarge()
    {
        var json = new string(' ', ThemeFile.MaxLength + 1);

        ThemeFile.TryParse("{" + json + "}", out _).Should().Be(ThemeFileError.TooLarge);
    }
}
