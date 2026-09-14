namespace K7.Clients.ComponentTests;

[TestFixture]
public class ThemeCssTokenTests
{
    private static readonly string[] RequiredTokens =
    [
        "--color-text:",
        "--color-text-primary:",
        "--color-text-on-media:",
        "--color-text-on-media-muted:",
        "--color-text-on-media-accent:",
        "--color-accent-text:",
        "--media-scrim:",
        "--color-on-primary:",
        "--color-surface-variant:",
        "--color-surface-container:",
        "--color-surface-raised:",
    ];

    [TestCase("default-light.css")]
    [TestCase("default-dark.css")]
    public void ThemeFile_ShouldDefineSharedTextAndSurfaceTokens_WhenBothThemesShip(string fileName)
    {
        var css = ReadTheme(fileName);

        foreach (var token in RequiredTokens)
            css.Should().Contain(token);
    }

    [Test]
    public void LightTheme_ShouldDefineReadableAccentText_WhenUsedOnSurfaces()
    {
        var css = ReadTheme("default-light.css");

        css.Should().Contain("--color-accent-text:");
        css.Should().Contain("#6e4b16");
        css.Should().NotContain("#837f70");
        css.Should().NotContain("rgba(243, 236, 222, 0.78)");
    }

    private static string ReadTheme(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "themes", fileName);
        File.Exists(path).Should().BeTrue($"theme file {fileName} should copy to test output");
        return File.ReadAllText(path);
    }
}
