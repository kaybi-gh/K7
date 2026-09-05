using AwesomeAssertions;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Server.Application.UnitTests;

public class ImportMediaTypeCompatibilityTests
{
    [Test]
    public void IsCompatible_ShouldAcceptSameKind()
    {
        ImportMediaTypeCompatibility.IsCompatible("episode", MediaType.SerieEpisode).Should().BeTrue();
        ImportMediaTypeCompatibility.IsCompatible("movie", MediaType.Movie).Should().BeTrue();
        ImportMediaTypeCompatibility.IsCompatible("music", MediaType.MusicTrack).Should().BeTrue();
        ImportMediaTypeCompatibility.IsCompatible("serie", MediaType.Serie).Should().BeTrue();
    }

    [Test]
    public void IsCompatible_ShouldRejectEpisodeBoundToSeriesOrMovie()
    {
        ImportMediaTypeCompatibility.IsCompatible("episode", MediaType.Serie).Should().BeFalse();
        ImportMediaTypeCompatibility.IsCompatible("episode", MediaType.Movie).Should().BeFalse();
        ImportMediaTypeCompatibility.IsCompatible("episode", MediaType.SerieSeason).Should().BeFalse();
    }
}
