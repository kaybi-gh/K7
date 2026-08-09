using FluentAssertions;
using K7.Server.Application.Common;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

public class MetadataProviderHostMapperTests
{
    [TestCase("image.tmdb.org", MetadataProviderNames.Tmdb)]
    [TestCase("artworks.thetvdb.com", MetadataProviderNames.Tvdb)]
    [TestCase("commons.wikimedia.org", MetadataProviderNames.Wikimedia)]
    [TestCase("upload.wikimedia.org", MetadataProviderNames.Wikimedia)]
    [TestCase("fr.wikipedia.org", MetadataProviderNames.Wikimedia)]
    [TestCase("coverartarchive.org", MetadataProviderNames.CoverArt)]
    [TestCase("archive.org", MetadataProviderNames.CoverArt)]
    [TestCase("ia601408.us.archive.org", MetadataProviderNames.CoverArt)]
    [TestCase("musicbrainz.org", MetadataProviderNames.MusicBrainz)]
    [TestCase("www.wikidata.org", MetadataProviderNames.Wikidata)]
    [TestCase("unknown.example", MetadataProviderNames.Local)]
    public void FromHost_ShouldMapKnownHosts(string host, string expected)
        => MetadataProviderHostMapper.FromHost(host).Should().Be(expected);

    [TestCase("tmdb", MetadataProviderNames.Tmdb)]
    [TestCase("themoviedb", MetadataProviderNames.Tmdb)]
    [TestCase("tvdb", MetadataProviderNames.Tvdb)]
    [TestCase("musicbrainz", MetadataProviderNames.MusicBrainz)]
    [TestCase(null, MetadataProviderNames.Local)]
    public void NormalizeProviderName_ShouldNormalize(string? name, string expected)
        => MetadataProviderHostMapper.NormalizeProviderName(name).Should().Be(expected);

    [Test]
    public void NormalizeForBackgroundTask_ShouldReturnNull_WhenLaneIsNotMetadata()
        => MetadataProviderHostMapper.NormalizeForBackgroundTask(BackgroundTaskLane.Probe, "tmdb")
            .Should().BeNull();

    [Test]
    public void NormalizeForBackgroundTask_ShouldNormalize_WhenLaneIsMetadata()
        => MetadataProviderHostMapper.NormalizeForBackgroundTask(BackgroundTaskLane.Metadata, " TMDB ")
            .Should().Be(MetadataProviderNames.Tmdb);

    [Test]
    public void NormalizeForBackgroundTask_ShouldThrow_WhenMetadataLaneAndProviderMissing()
    {
        var act = () => MetadataProviderHostMapper.NormalizeForBackgroundTask(BackgroundTaskLane.Metadata, null);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("metadataProviderName");
    }
}
