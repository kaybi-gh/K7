using K7.Shared.Parsing;

namespace K7.Server.Application.UnitTests.Parsing;

[TestFixture]
public class EpisodeRangeParserTests
{
    [TestCase("Show.S01E01.mkv", 1, 1, 1)]
    [TestCase("Show S1E1", 1, 1, 1)]
    [TestCase("s01e01", 1, 1, 1)]
    [TestCase("Show.S07E025.mkv", 7, 25, 25)]
    public void TryParse_ShouldReturnSingleEpisode_WhenNoRange(string text, int season, int first, int last)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.Season.Should().Be(season);
        result.FirstEpisode.Should().Be(first);
        result.LastEpisode.Should().Be(last);
    }

    [TestCase("S01E01-E03", 1, 1, 3)]
    [TestCase("S01E01-E04", 1, 1, 4)]
    [TestCase("S01E01E02", 1, 1, 2)]
    [TestCase("S01E01e02e03", 1, 1, 3)]
    [TestCase("S07E025-E026", 7, 25, 26)]
    [TestCase("S01E01-03", 1, 1, 3)]
    [TestCase("S01E01~E02", 1, 1, 2)]
    [TestCase("S01E01+E02", 1, 1, 2)]
    [TestCase("S01E01-S01E02", 1, 1, 2)]
    [TestCase("S1E1-S1E2", 1, 1, 2)]
    [TestCase("S1E01-S01E02", 1, 1, 2)]
    [TestCase("S1E01-S01E1", 1, 1, 1)]
    [TestCase("S1E1-S1E2-S1E3-S1E4", 1, 1, 4)]
    [TestCase("S1E1-S1E3-S1E5", 1, 1, 5)]
    [TestCase("S01E01 - S01E02", 1, 1, 2)]
    [TestCase("Show.S01E01-S01E02.720p", 1, 1, 2)]
    [TestCase("The Office (US) - S07E025-E026 - Search Committee.mkv", 7, 25, 26)]
    public void TryParse_ShouldReturnInclusiveMinMax_WhenSameSeasonRange(string text, int season, int first, int last)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.Season.Should().Be(season);
        result.FirstEpisode.Should().Be(first);
        result.LastEpisode.Should().Be(last);
    }

    [TestCase("1x01", 1, 1, 1)]
    [TestCase("1x01-03", 1, 1, 3)]
    [TestCase("1x01-1x02", 1, 1, 2)]
    [TestCase("2x05-2x07", 2, 5, 7)]
    [TestCase("S01E01-1x03", 1, 1, 3)]
    [TestCase("1x01-S01E04", 1, 1, 4)]
    [TestCase("S01E01-E02-1x03", 1, 1, 3)]
    public void TryParse_ShouldReturnRange_WhenNxNNOrMixedNotation(string text, int season, int first, int last)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.Season.Should().Be(season);
        result.FirstEpisode.Should().Be(first);
        result.LastEpisode.Should().Be(last);
    }

    [TestCase("S01E01-S02E01", 1, 1, 1)]
    [TestCase("S01E02-S02E01", 1, 2, 2)]
    [TestCase("1x01-2x01", 1, 1, 1)]
    public void TryParse_ShouldKeepFirstEpisodeOnly_WhenSeasonChanges(string text, int season, int first, int last)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.Season.Should().Be(season);
        result.FirstEpisode.Should().Be(first);
        result.LastEpisode.Should().Be(last);
    }

    [TestCase("S01E01.720p")]
    [TestCase("S01E01-720p")]
    [TestCase("S01E01.1080p.mkv")]
    public void TryParse_ShouldNotTreatResolutionAsRange(string text)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.FirstEpisode.Should().Be(1);
        result.LastEpisode.Should().Be(1);
    }

    [TestCase("S00E01-E02", 0, 1, 2)]
    public void TryParse_ShouldAllowSpecialsSeason(string text, int season, int first, int last)
    {
        EpisodeRangeParser.TryParse(text, out var result).Should().BeTrue();
        result.Season.Should().Be(season);
        result.FirstEpisode.Should().Be(first);
        result.LastEpisode.Should().Be(last);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Movie.1080p.BluRay.mkv")]
    [TestCase("Show Name - 1001")]
    public void TryParse_ShouldReturnFalse_WhenNoSeasonEpisodeToken(string? text)
    {
        EpisodeRangeParser.TryParse(text, out _).Should().BeFalse();
    }
}
