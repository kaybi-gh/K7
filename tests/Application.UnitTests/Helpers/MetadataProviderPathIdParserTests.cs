using K7.Server.Application.Common;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataProviderPathIdParserTests
{
    [Test]
    public void TryParse_ShouldExtractTmdbId_FromBracketToken()
    {
        var (provider, id) = MetadataProviderPathIdParser.TryParse("The Buccaneers (2023) [tmdbid-213338]");
        provider.Should().Be(MetadataProviderNames.Tmdb);
        id.Should().Be("213338");
    }

    [Test]
    public void TryParse_ShouldExtractTvdbId()
    {
        var (provider, id) = MetadataProviderPathIdParser.TryParse(@"D:\Shows\Bull (2016) [tvdbid-305151]\Season 01");
        provider.Should().Be(MetadataProviderNames.Tvdb);
        id.Should().Be("305151");
    }

    [Test]
    public void TryParse_ShouldExtractImdbId()
    {
        var (provider, id) = MetadataProviderPathIdParser.TryParse("Show [imdbid-tt1234567]");
        provider.Should().Be(MetadataProviderNames.Imdb);
        id.Should().Be("tt1234567");
    }

    [Test]
    public void StripProviderIdTokens_ShouldRemoveBracketIdsFromTitle()
    {
        var cleaned = MetadataProviderPathIdParser.StripProviderIdTokens("The Buccaneers (2023) [tmdbid-213338]");
        cleaned.Should().Be("The Buccaneers (2023)");
    }

    [Test]
    public void TryParseFromPaths_ShouldPreferFirstMatch()
    {
        var (provider, id) = MetadataProviderPathIdParser.TryParseFromPaths(
            "S01E01.mkv",
            "The Buccaneers (2023) [tmdbid-213338]");
        provider.Should().Be(MetadataProviderNames.Tmdb);
        id.Should().Be("213338");
    }
}
