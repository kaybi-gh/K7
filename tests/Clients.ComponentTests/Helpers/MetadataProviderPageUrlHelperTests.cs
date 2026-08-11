using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MetadataProviderPageUrlHelperTests
{
    [Test]
    public void TryBuild_ShouldReturnMusicBrainzReleaseGroupUrl()
    {
        MetadataProviderPageUrlHelper.TryBuild("musicbrainz", "abc-123", MediaType.MusicAlbum)
            .Should().Be("https://musicbrainz.org/release-group/abc-123");
    }

    [Test]
    public void TryBuild_ShouldReturnTmdbMovieUrl_WhenMovie()
    {
        MetadataProviderPageUrlHelper.TryBuild("tmdb", "550", MediaType.Movie)
            .Should().Be("https://www.themoviedb.org/movie/550");
    }

    [Test]
    public void TryBuild_ShouldReturnTmdbTvUrl_WhenSerie()
    {
        MetadataProviderPageUrlHelper.TryBuild("tmdb", "1396", MediaType.Serie)
            .Should().Be("https://www.themoviedb.org/tv/1396");
    }

    [Test]
    public void TryBuild_ShouldReturnTvdbDereferrerUrl()
    {
        MetadataProviderPageUrlHelper.TryBuild("tvdb", "78804", MediaType.Serie)
            .Should().Be("https://www.thetvdb.com/dereferrer/series/78804");
    }

    [Test]
    public void TryBuild_ShouldReturnNull_WhenProviderUnknown()
    {
        MetadataProviderPageUrlHelper.TryBuild("federation", "x", MediaType.Movie)
            .Should().BeNull();
    }
}
