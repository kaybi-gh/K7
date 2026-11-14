using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class BuiltInThemesTests
{
    private static IEnumerable<ThemeDefinition> Themes => BuiltInThemes.All;

    [TestCaseSource(nameof(Themes))]
    public void Validate_ShouldReportNoIssue_WhenThemeIsBuiltIn(ThemeDefinition theme)
    {
        ThemePaletteValidator.Validate(theme.Palette).Should().BeEmpty($"{theme.Id} must stay readable");
    }

    [Test]
    public void All_ShouldHaveUniqueIds()
    {
        BuiltInThemes.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }

    [TestCase(BuiltInThemes.DefaultDarkId, "default-dark.css")]
    [TestCase(BuiltInThemes.DefaultLightId, "default-light.css")]
    public void BasePalette_ShouldMatchThemeStylesheet_WhenThemeIsABase(string themeId, string fileName)
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "themes", fileName));
        var palette = BuiltInThemes.Find(themeId)!.Palette;

        css.Should().MatchRegex($@"--color-bg:\s*{palette.Background}");
        css.Should().MatchRegex($@"--color-surface:\s*{palette.Surface}");
        css.Should().MatchRegex($@"--color-surface-2:\s*{palette.SurfaceAlt}");
        css.Should().MatchRegex($@"--color-text:\s*{palette.Text}");
        css.Should().MatchRegex($@"--color-text-secondary:\s*{palette.TextSecondary}");
        css.Should().MatchRegex($@"--color-text-muted:\s*{palette.TextMuted}");
        css.Should().MatchRegex($@"--color-accent:\s*{palette.Accent}");
        css.Should().MatchRegex($@"--color-accent-fg:\s*{palette.OnAccent}");
    }

    [Test]
    public void Find_ShouldReturnNull_WhenIdIsUnknown()
    {
        BuiltInThemes.Find("custom-abc").Should().BeNull();
        BuiltInThemes.Find(null).Should().BeNull();
    }
}
