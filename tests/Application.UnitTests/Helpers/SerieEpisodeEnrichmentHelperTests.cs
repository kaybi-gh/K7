using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class SerieEpisodeEnrichmentHelperTests
{
    [Test]
    public void IsUnenriched_ShouldBeTrue_WhenPlaceholderTitleAndNoProviderData()
    {
        var episode = new SerieEpisode { EpisodeNumber = 4, Title = "Episode 4" };

        SerieEpisodeEnrichmentHelper.IsUnenriched(episode).Should().BeTrue();
    }

    [Test]
    public void IsUnenriched_ShouldBeFalse_WhenTitleIsReal()
    {
        var episode = new SerieEpisode { EpisodeNumber = 1, Title = "Pilot" };

        SerieEpisodeEnrichmentHelper.IsUnenriched(episode).Should().BeFalse();
    }

    [Test]
    public void IsUnenriched_ShouldBeFalse_WhenPlaceholderTitleHasOverview()
    {
        var episode = new SerieEpisode
        {
            EpisodeNumber = 1,
            Title = "Episode 1",
            Overview = "Provider overview"
        };

        SerieEpisodeEnrichmentHelper.IsUnenriched(episode).Should().BeFalse();
    }

    [Test]
    public void IsSeasonUnenriched_ShouldBeFalse_WhenSeasonHasOverview()
    {
        var season = new SerieSeason
        {
            SeasonNumber = 1,
            Title = "Season 1",
            Overview = "The first season"
        };

        SerieEpisodeEnrichmentHelper.IsSeasonUnenriched(season).Should().BeFalse();
    }

    [Test]
    public void RemoveExistingPictureTypes_ShouldKeepMissingTypesOnly()
    {
        var serie = new Serie { Title = "Show" };
        serie.Pictures.Add(new MetadataPicture { Type = MetadataPictureType.Poster });

        var incoming = new List<MetadataPicture>
        {
            new() { Type = MetadataPictureType.Poster, OriginalRemoteUri = new Uri("https://img.example/poster.jpg") },
            new() { Type = MetadataPictureType.Backdrop, OriginalRemoteUri = new Uri("https://img.example/back.jpg") }
        };

        SerieEpisodeEnrichmentHelper.RemoveExistingPictureTypes(serie, incoming);

        incoming.Should().ContainSingle(picture => picture.Type == MetadataPictureType.Backdrop);
    }
}
