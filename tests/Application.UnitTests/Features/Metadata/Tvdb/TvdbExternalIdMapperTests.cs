using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbExternalIdMapperTests
{
    [TestCase("IMDB", "imdb")]
    [TestCase("TheMovieDB", "tmdb")]
    [TestCase("TV Maze", "tvmaze")]
    [TestCase("EIDR", "eidr")]
    [TestCase("Wikidata", "wikidata")]
    [TestCase("Wikipedia", "wikipedia")]
    public void BuildExternalIds_ShouldMapKnownRemoteSources(string sourceName, string expectedProvider)
    {
        var ids = TvdbExternalIdMapper.BuildExternalIds("1", [new TvdbRemoteId { Id = "value", SourceName = sourceName }]);

        ids.Should().ContainSingle(i => i.ProviderName == expectedProvider && i.Value == "value");
    }

    [Test]
    public void BuildExternalIds_ShouldIgnoreUnknownRemoteSources()
    {
        var ids = TvdbExternalIdMapper.BuildExternalIds("1", [new TvdbRemoteId { Id = "x", SourceName = "Unknown Provider" }]);

        ids.Should().ContainSingle().Which.ProviderName.Should().Be("tvdb");
    }

    [Test]
    public void BuildExternalIds_ShouldIncludePrimaryTvdbAndMappedRemoteIds()
    {
        var remoteIds = new List<TvdbRemoteId>
        {
            new() { Id = "tt1234567", SourceName = "IMDB" },
            new() { Id = "1396", SourceName = "TheMovieDB" },
            new() { Id = "169", SourceName = "TV Maze" },
            new() { Id = "10.5240/ABC", SourceName = "EIDR" },
            new() { Id = "Q123", SourceName = "Wikidata" },
            new() { Id = "Breaking_Bad", SourceName = "Wikipedia" },
            new() { Id = "duplicate", SourceName = "IMDB" }
        };

        var ids = TvdbExternalIdMapper.BuildExternalIds("102621", remoteIds);

        ids.Should().HaveCount(7);
        ids[0].ProviderName.Should().Be("tvdb");
        ids[0].Value.Should().Be("102621");
        ids.Select(i => i.ProviderName).Should().BeEquivalentTo(
            ["tvdb", "imdb", "tmdb", "tvmaze", "eidr", "wikidata", "wikipedia"]);
    }
}
