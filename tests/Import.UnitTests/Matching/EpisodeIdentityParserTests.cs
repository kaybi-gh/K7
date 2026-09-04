using K7.Import.Matching;

namespace K7.Import.UnitTests.Matching;

[TestFixture]
public class EpisodeIdentityParserTests
{
    [Test]
    public void TryParseFromPath_ShouldReadLeadingEpisode_WhenSeasonFolderIsSaisonN()
    {
        var parsed = EpisodeIdentityParser.TryParseFromPath(
            "/media/series/Game of throne/Saison 5/05 - Tue L Enfant.mkv",
            out var seriesTitle,
            out var season,
            out var episode,
            out var lastEpisode);

        parsed.Should().BeTrue();
        seriesTitle.Should().Be("Game of throne");
        season.Should().Be(5);
        episode.Should().Be(5);
        lastEpisode.Should().Be(5);
    }

    [Test]
    public void TryParseFromPath_ShouldReadReleaseStyleSeasonFolder()
    {
        var parsed = EpisodeIdentityParser.TryParseFromPath(
            "/media/series/Warehouse 13/Warehouse 13 - Saison 01 - DVDRip TrueFrench - Chupacabra/01 - Bienvenue.mp4",
            out var seriesTitle,
            out var season,
            out var episode,
            out _);

        parsed.Should().BeTrue();
        seriesTitle.Should().Be("Warehouse 13");
        season.Should().Be(1);
        episode.Should().Be(1);
    }
}
