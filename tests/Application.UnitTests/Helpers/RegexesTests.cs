using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class RegexesTests
{
    [TestCase("Season 1", 1)]
    [TestCase("Saison 2", 2)]
    [TestCase("S01", 1)]
    [TestCase("S4", 4)]
    [TestCase("Sur le front S04", 4)]
    [TestCase("Taratata S25", 25)]
    [TestCase("Specials", 0)]
    [TestCase("Extras", 0)]
    public void TryParseSeasonFolder_ShouldParseKnownLayouts(string folderName, int expectedSeason)
    {
        var parsed = Regexes.TryParseSeasonFolder(folderName, out var season);

        parsed.Should().BeTrue();
        season.Should().Be(expectedSeason);
    }

    [TestCase("DOCS & TV")]
    [TestCase("Random Folder")]
    [TestCase("")]
    public void TryParseSeasonFolder_ShouldReturnFalse_WhenNotASeasonFolder(string folderName)
    {
        var parsed = Regexes.TryParseSeasonFolder(folderName, out _);

        parsed.Should().BeFalse();
    }
}
