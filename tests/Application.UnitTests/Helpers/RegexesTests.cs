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
    [TestCase("Warehouse 13 - Saison 01 - DVDRip TrueFrench - Chupacabra", 1)]
    [TestCase("Show Name - Season 2 - 1080p BluRay", 2)]
    [TestCase("Saison 5", 5)]
    public void TryParseSeasonFolder_ShouldParseKnownLayouts(string folderName, int expectedSeason)
    {
        var parsed = Regexes.TryParseSeasonFolder(folderName, out var season);

        parsed.Should().BeTrue();
        season.Should().Be(expectedSeason);
    }

    [TestCase("DOCS & TV")]
    [TestCase("Random Folder")]
    [TestCase("Warehouse 13")]
    [TestCase("The Series 2")]
    [TestCase("")]
    public void TryParseSeasonFolder_ShouldReturnFalse_WhenNotASeasonFolder(string folderName)
    {
        var parsed = Regexes.TryParseSeasonFolder(folderName, out _);

        parsed.Should().BeFalse();
    }

    [TestCase("Warehouse 13 - Saison 01 - DVDRip TrueFrench - Chupacabra", "Warehouse 13")]
    [TestCase("Show Name - Season 2 - 1080p", "Show Name")]
    [TestCase("Show Name S04", "Show Name S04")]
    [TestCase("Saison 5", null)]
    [TestCase("Season 01", null)]
    public void StripSeasonFolderDecorations_ShouldKeepShowName_WhenPresent(string folderName, string? expected)
    {
        Regexes.StripSeasonFolderDecorations(folderName).Should().Be(expected);
    }

    [TestCase("05 - Tue L Enfant", 5)]
    [TestCase("01 - Bienvenue - 1ere Partie - Chupacabra", 1)]
    [TestCase("1. Pilot", 1)]
    [TestCase("100 - Century", 100)]
    public void TryParseLeadingEpisodeNumber_ShouldParseSceneStyleNames(string fileName, int expectedEpisode)
    {
        var parsed = Regexes.TryParseLeadingEpisodeNumber(fileName, out var episode);

        parsed.Should().BeTrue();
        episode.Should().Be(expectedEpisode);
    }

    [TestCase("Show - S01E01")]
    [TestCase("Bleach - 50")]
    [TestCase("05")]
    [TestCase("")]
    public void TryParseLeadingEpisodeNumber_ShouldReturnFalse_WhenNotLeadingEpisode(string fileName)
    {
        Regexes.TryParseLeadingEpisodeNumber(fileName, out _).Should().BeFalse();
    }
}
