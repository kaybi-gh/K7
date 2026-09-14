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
        "--color-on-fill:",
        "--color-error-fill:",
        "--color-success-fill:",
        "--color-warning-fill:",
        "--color-surface-variant:",
        "--color-surface-container:",
        "--color-surface-raised:",
        "--shadow-color:",
        "--media-copy-shadow:",
        "--media-watched-chip:",
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
        css.Should().MatchRegex(@"--color-accent:\s*#d9b060");
        css.Should().MatchRegex(@"--color-accent-hover:\s*#e8c878");
        css.Should().NotContain("#837f70");
        css.Should().NotContain("#a67c32");
        css.Should().NotContain("#8a5f1f");
        css.Should().NotContain("rgba(243, 236, 222, 0.78)");
    }

    [Test]
    public void BothThemes_ShouldShareTheSamePrimaryGold()
    {
        ReadTheme("default-light.css").Should().MatchRegex(@"--color-primary:\s*#d9b060");
        ReadTheme("default-dark.css").Should().MatchRegex(@"--color-primary:\s*#d9b060");
        ReadTheme("default-light.css").Should().MatchRegex(@"--color-primary-hover:\s*#e8c878");
        ReadTheme("default-dark.css").Should().MatchRegex(@"--color-primary-hover:\s*#e8c878");
    }

    private static string ReadTheme(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "themes", fileName);
        File.Exists(path).Should().BeTrue($"theme file {fileName} should copy to test output");
        return File.ReadAllText(path);
    }
}
