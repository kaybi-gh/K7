using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Theming;

[TestFixture]
public class ServerThemesTests
{
    private static CustomThemeDto Theme(string id = "server-abc", bool isDark = true) => new()
    {
        Id = id,
        Name = "House",
        Palette = (isDark ? BuiltInThemes.Midnight : BuiltInThemes.Paper).Palette with { }
    };

    [Test]
    public void IsValid_ShouldPass_WhenThemesHaveServerIdsNamesAndColors()
    {
        ServerThemes.IsValid([Theme(), Theme("server-def")]).Should().BeTrue();
    }

    [TestCase("custom-abc")]
    [TestCase("server-")]
    [TestCase("server-<script>")]
    [TestCase("midnight")]
    public void IsValid_ShouldFail_WhenIdIsNotAServerThemeId(string id)
    {
        ServerThemes.IsValid([Theme(id)]).Should().BeFalse();
    }

    [Test]
    public void IsValid_ShouldFail_WhenIdsAreDuplicated()
    {
        ServerThemes.IsValid([Theme(), Theme()]).Should().BeFalse();
    }

    [Test]
    public void IsValid_ShouldFail_WhenAColorIsNotHex()
    {
        var theme = Theme();
        theme.Palette.Accent = "red;background:url(x)";

        ServerThemes.IsValid([theme]).Should().BeFalse();
    }

    [Test]
    public void IsValid_ShouldFail_WhenThereAreTooManyThemes()
    {
        var themes = Enumerable.Range(0, ServerThemes.MaxCount + 1).Select(i => Theme($"server-{i}")).ToList();

        ServerThemes.IsValid(themes).Should().BeFalse();
    }

    [Test]
    public void Parse_ShouldReturnWhatSerializeWrote()
    {
        var themes = new List<CustomThemeDto> { Theme(), Theme("server-def", isDark: false) };

        ServerThemes.Parse(ServerThemes.Serialize(themes)).Should().BeEquivalentTo(themes);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{}")]
    public void Parse_ShouldReturnNothing_WhenStoredValueIsUnreadable(string? json)
    {
        ServerThemes.Parse(json).Should().BeEmpty();
    }

    [Test]
    public void Parse_ShouldLeaveOutThemesThatAreNotValid()
    {
        var broken = Theme("server-bad");
        broken.Palette.Background = "nope";

        var parsed = ServerThemes.Parse(ServerThemes.Serialize([Theme(), broken]));

        parsed.Select(t => t.Id).Should().Equal("server-abc");
    }

    [Test]
    public void BaseAttribute_ShouldFollowThePaletteOfTheDefaultTheme()
    {
        var themes = new List<CustomThemeDto> { Theme("server-light", isDark: false), Theme("server-dark") };

        ServerThemes.BaseAttribute("server-light", themes).Should().Be(BuiltInThemes.DefaultLightId);
        ServerThemes.BaseAttribute("server-dark", themes).Should().Be(BuiltInThemes.DefaultDarkId);
        ServerThemes.BaseAttribute("paper", themes).Should().Be(BuiltInThemes.DefaultLightId);
        ServerThemes.BaseAttribute("server-gone", themes).Should().Be(BuiltInThemes.DefaultDarkId);
    }
}
